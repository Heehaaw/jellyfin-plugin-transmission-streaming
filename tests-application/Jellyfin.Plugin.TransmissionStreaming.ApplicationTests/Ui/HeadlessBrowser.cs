namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Ui;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Hosting;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

/// <summary>Reuses one containerized Chromium process while isolating each trial in a fresh browser context.</summary>
public sealed class HeadlessBrowser : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/playwright:v1.64.1-noble@sha256:47b510dd9230bf0b9bfc9b20102241e577ea7a99334ab4c3ca19f19770e9b8ea";
    private const int ServerPort = 3000;
    private IContainer? container;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private Task? startup;

    /// <summary>Leaves browser infrastructure stopped until a UI trial needs it.</summary>
    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    internal async Task VerifySettingsNavigationAsync(INetwork network, string pluginName, CancellationToken cancellationToken)
    {
        // Container scenarios are serialized by the assembly's exclusive downloader lease.
        startup ??= StartAsync(network, cancellationToken);
        await startup;
        await using var context = await browser!.NewContextAsync(
            new()
            {
                ViewportSize = new()
                {
                    Width = 1440,
                    Height = 1000
                }
            }
        );
        context.SetDefaultTimeout(15_000);
        var page = await context.NewPageAsync();
        var dashboard = new JellyfinDashboard(page);
        await dashboard.SignInAsync(cancellationToken);
        var plugins = page.GetByRole(
            role: AriaRole.List,
            options: new()
            {
                Name = "Plugins",
                Exact = true
            }
        );
        var link = plugins.GetByRole(
            role: AriaRole.Link,
            options: new()
            {
                Name = pluginName,
                Exact = true
            }
        );
        await Expect(link).ToBeVisibleAsync();
        await Expect(link).ToHaveCountAsync(1);
        await link.ClickAsync();
        var settings = page.Locator("#transmissionStreamingSettings");
        await Expect(settings).ToBeVisibleAsync();
        var heading = settings.GetByRole(
            role: AriaRole.Heading,
            options: new()
            {
                Name = pluginName,
                Exact = true
            }
        );
        await Expect(heading).ToBeVisibleAsync();
        var expectedStatus = "Scanning disabled";
        await Expect(settings.Locator("#streamingStatus")).ToHaveTextAsync(expectedStatus);
    }

    private async Task StartAsync(INetwork network, CancellationToken cancellationToken)
    {
        container = new ContainerBuilder(Image)
            .WithCreateParameterModifier(parameters => parameters.User = "pwuser")
            .WithWorkingDirectory("/home/pwuser")
            .WithNetwork(network)
            .WithPortBinding(ServerPort, true)
            .WithCommand("sh", "-c", "npx -y playwright@1.64.1 run-server --port 3000 --host 0.0.0.0")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ServerPort))
            .Build();
        await container.StartAsync(cancellationToken);
        playwright = await Playwright.CreateAsync();
        var endpoint = new UriBuilder("ws", container.Hostname, container.GetMappedPublicPort(ServerPort)).Uri;
        browser = await playwright.Chromium.ConnectAsync(
            endpoint: endpoint.AbsoluteUri,
            options: new()
            {
                Timeout = 30_000
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (browser is not null)
            {
                await browser.DisposeAsync();
            }
        }
        finally
        {
            playwright?.Dispose();
            if (container is not null)
            {
                await container.DisposeAsync();
            }
        }
    }
}
