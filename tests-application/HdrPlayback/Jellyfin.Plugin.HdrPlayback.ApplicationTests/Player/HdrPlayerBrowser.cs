namespace Jellyfin.Plugin.HdrPlayback.ApplicationTests.Player;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Hosting;
using Microsoft.Playwright;
using System.Text.Json.Nodes;
using static Microsoft.Playwright.Assertions;

/// <summary>Exercises Jellyfin's real player menu in an isolated Chromium container.</summary>
internal sealed class HdrPlayerBrowser : IAsyncDisposable
{
    private const string Image = "mcr.microsoft.com/playwright:v1.64.1-noble@sha256:47b510dd9230bf0b9bfc9b20102241e577ea7a99334ab4c3ca19f19770e9b8ea";
    private IContainer? container;
    private IPlaywright? playwright;
    private IBrowser? browser;

    public async Task VerifyMenuAsync(HdrJellyfinFixture server, CancellationToken cancellationToken)
    {
        await StartAsync(server, cancellationToken);
        await VerifyVideoSelectionAsync(server);
        var errors = new List<string>();
        foreach (var viewport in new[] { new ViewportSize { Width = 1600, Height = 1000 }, new ViewportSize { Width = 390, Height = 844 } })
        {
            // A fresh context evaluates Jellyfin's responsive/player state at the intended size without dismissing an open native sheet.
            await using var context = await browser!.NewContextAsync(new BrowserNewContextOptions { ViewportSize = viewport });
            context.SetDefaultTimeout(20_000);
            var page = await context.NewPageAsync();
            page.PageError += (_, error) => errors.Add(viewport.Width + ": " + error);
            await PlayAsync(page, server);
            await page.Mouse.MoveAsync(viewport.Width / 2, viewport.Height / 2);
            await page.Locator(".btnVideoOsdSettings:visible").ClickAsync();
            await Expect(MenuItem(page, "HDR conversion")).ToBeVisibleAsync();
            await Expect(MenuItem(page, "Quality")).ToBeVisibleAsync();
            if (viewport.Width > 600)
            {
                await MenuItem(page, "Quality").ClickAsync();
                // Target the native bitrate ID; the closing parent sheet also contains an Auto aside label.
                var automatic = page.Locator(".actionSheetMenuItem[data-id='0']");
                await Expect(automatic).ToBeVisibleAsync();
                await automatic.ClickAsync();
                await Expect(automatic).ToHaveCountAsync(0);
            }
            else
            {
                var explanation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                page.Dialog += async (_, dialog) =>
                {
                    explanation.TrySetResult(dialog.Message);
                    await dialog.AcceptAsync();
                };
                await MenuItem(page, "HDR conversion").ClickAsync();
                var unavailableReason = await explanation.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                unavailableReason.Should().Contain("VA-API");
                await Expect(MenuItem(page, "HDR conversion")).ToHaveCountAsync(0);
            }
        }

        await using var convertedContext = await browser!.NewContextAsync();
        var convertedPage = await convertedContext.NewPageAsync();
        convertedPage.PageError += (_, error) => errors.Add("converted player: " + error);
        // Isolate the UI contract from this CPU-only server's hardware rejection. Real HDR encoding is a separate GPU check.
        await convertedPage.RouteAsync("**/PlaybackInfo", async route =>
        {
            var response = await route.FetchAsync();
            var headers = new Dictionary<string, string>(response.Headers, StringComparer.OrdinalIgnoreCase)
            {
                ["X-Jellyfin-Hdr-Source"] = "true"
            };
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Response = response,
                Headers = headers
            });
        });
        await PlayAsync(convertedPage, server);
        await convertedPage.Mouse.MoveAsync(640, 360);
        await convertedPage.Locator(".btnVideoOsdSettings:visible").ClickAsync();
        await Expect(MenuItem(convertedPage, "Quality")).ToBeVisibleAsync();
        await Expect(MenuItem(convertedPage, "HDR conversion")).ToHaveCountAsync(0);
        await MenuItem(convertedPage, "Quality").ClickAsync();
        var convertedAutomatic = convertedPage.Locator(".actionSheetMenuItem[data-id='0']");
        await Expect(convertedAutomatic).ToBeVisibleAsync();
        await convertedAutomatic.ClickAsync();
        await Expect(convertedAutomatic).ToHaveCountAsync(0);
        errors.Should().BeEmpty("the extended native menu must run without JavaScript exceptions");
    }

    private async Task VerifyVideoSelectionAsync(HdrJellyfinFixture server)
    {
        await using var context = await browser!.NewContextAsync();
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(10_000);
        // The real CPU-only host hides conversion choices. Supply the advertised DTO to exercise its real details view.
        await page.RouteAsync("**/Items/" + server.MovieId + "*", async route =>
        {
            var response = await route.FetchAsync();
            var body = JsonNode.Parse(await response.TextAsync())!;
            var sources = body["MediaSources"]?.AsArray();
            if (sources is { Count: > 0 })
            {
                var converted = sources[0]!.DeepClone();
                converted["Id"] = "hdr-conversion:" + sources[0]!["Id"]!.GetValue<string>();
                converted["Name"] = "HDR (converted)";
                converted["SupportsDirectPlay"] = false;
                converted["SupportsDirectStream"] = false;
                converted["TranscodingUrl"] = null;
                sources.Add(converted);
                body["MediaSourceCount"] = sources.Count;
            }

            var headers = new Dictionary<string, string>(response.Headers, StringComparer.OrdinalIgnoreCase);
            headers.Remove("content-length");
            headers.Remove("content-encoding");
            await route.FulfillAsync(new RouteFulfillOptions { Response = response, Headers = headers, Body = body.ToJsonString() });
        });
        await SignInAsync(page, server);
        await page.GotoAsync($"{HdrJellyfinFixture.InternalAddress}/web/#/details?id={server.MovieId}&serverId={server.ServerId}");
        var video = page.Locator(".selectVideo:visible");
        await Expect(video).ToBeEnabledAsync();
        await Expect(video.Locator("option")).ToHaveCountAsync(2);
        var originalTrack = await video.InputValueAsync();
        var source = page.Locator(".selectSource");
        var originalSource = await source.InputValueAsync();
        var audio = await page.Locator(".selectAudio").InputValueAsync();
        var subtitles = await page.Locator(".selectSubtitles").InputValueAsync();
        await video.SelectOptionAsync(new SelectOptionValue { Label = "HDR (converted)" });
        await Expect(source).ToHaveValueAsync("hdr-conversion:" + originalSource);
        await Expect(page.Locator(".selectAudio")).ToHaveValueAsync(audio);
        await Expect(page.Locator(".selectSubtitles")).ToHaveValueAsync(subtitles);
        await video.SelectOptionAsync(originalTrack);
        await Expect(source).ToHaveValueAsync(originalSource);
        await Expect(video.Locator("option")).ToHaveCountAsync(2);
    }

    private async Task StartAsync(HdrJellyfinFixture server, CancellationToken cancellationToken)
    {
        container = new ContainerBuilder(Image)
            .WithCreateParameterModifier(parameters => parameters.User = "pwuser")
            .WithWorkingDirectory("/home/pwuser")
            .WithNetwork(server.Network)
            .WithPortBinding(3000, true)
            .WithCommand("sh", "-c", "npx -y playwright@1.64.1 run-server --port 3000 --host 0.0.0.0")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3000))
            .Build();
        await container.StartAsync(cancellationToken);
        playwright = await Playwright.CreateAsync();
        var endpoint = new UriBuilder("ws", container.Hostname, container.GetMappedPublicPort(3000)).Uri;
        browser = await playwright.Chromium.ConnectAsync(endpoint.AbsoluteUri);
    }

    private static async Task SignInAsync(IPage page, HdrJellyfinFixture server)
    {
        await page.GotoAsync(HdrJellyfinFixture.InternalAddress + "/web/#/login");
        await page.Locator("input[name=Username], #txtManualName").FillAsync("hdr-test-admin");
        await page.Locator("input[type=password]").FillAsync("Disposable-HDR-test-only-123!");
        await page.GetByRole(
            role: AriaRole.Button,
            options: new PageGetByRoleOptions
            {
                Name = "Sign In",
                Exact = true
            }
        ).ClickAsync();
        await page.WaitForURLAsync("**/home*");
    }

    private static async Task PlayAsync(IPage page, HdrJellyfinFixture server)
    {
        await SignInAsync(page, server);
        await page.GotoAsync($"{HdrJellyfinFixture.InternalAddress}/web/#/details?id={server.MovieId}&serverId={server.ServerId}");
        await page.Locator(".btnPlay:visible").First.ClickAsync();
        try
        {
            await Expect(page.Locator("video")).ToBeVisibleAsync();
            await page.WaitForFunctionAsync(
                "() => { const video = document.querySelector('video'); " +
                "return video && video.readyState >= 2 && video.videoWidth > 0 && !video.paused && video.currentTime > 0; }"
            );
        }
        catch (PlaywrightException)
        {
            throw new InvalidOperationException("Player startup failed. Server exceptions: " + await server.ErrorsAsync());
        }
    }

    private static ILocator MenuItem(IPage page, string name)
    {
        return page.GetByText(
            text: name,
            options: new PageGetByTextOptions
            {
                Exact = true
            }
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}
