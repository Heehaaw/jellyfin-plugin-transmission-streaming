namespace Jellyfin.Plugin.HdrPlayback.ApplicationTests.Hosting;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

/// <summary>Owns a disposable real Jellyfin server with both plugin bundles and synthetic SDR media.</summary>
public sealed class HdrJellyfinFixture : IAsyncLifetime
{
    private const string Image = "jellyfin/jellyfin:12.3@sha256:2038fe038ab3876a80f6e7b7e0aef6607b032c7dc0c9b3ad3296d0b2eba3bbb5";
    private readonly string workspace = Path.Combine(
        path1: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        path2: ".cache",
        path3: "hdr-playback-tests",
        path4: Guid.NewGuid().ToString("N")
    );
    private readonly INetwork network = new NetworkBuilder().Build();
    private IContainer? container;
    private HttpClient? client;
    private string? token;

    internal HttpClient Client => client!;
    internal INetwork Network => network;
    internal const string InternalAddress = "http://hdr-jellyfin:8096/jf";
    internal string ServerId { get; private set; } = "";
    internal string UserId { get; private set; } = "";
    internal string MovieId { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var configuration = Directory.CreateDirectory(Path.Combine(workspace, "config")).FullName;
        var media = Directory.CreateDirectory(Path.Combine(workspace, "media", "Reference (2026)")).FullName;
        var cache = Directory.CreateDirectory(Path.Combine(workspace, "cache")).FullName;
        InstallBundle(configuration, "TransmissionStreaming");
        InstallBundle(configuration, "HdrPlayback");
        var settings = Directory.CreateDirectory(Path.Combine(configuration, "config")).FullName;
        var networkConfiguration = "<NetworkConfiguration><BaseUrl>/jf</BaseUrl><EnableUPnP>false</EnableUPnP>" +
                                   "<EnableAutoDiscovery>false</EnableAutoDiscovery></NetworkConfiguration>";
        await File.WriteAllTextAsync(Path.Combine(settings, "network.xml"), networkConfiguration);
        await network.CreateAsync();
        container = new ContainerBuilder(Image)
            .WithNetwork(network)
            .WithNetworkAliases("hdr-jellyfin")
            .WithPortBinding(8096, true)
            .WithBindMount(configuration, "/config")
            .WithBindMount(Path.GetDirectoryName(media)!, "/media")
            .WithBindMount(cache, "/cache")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8096).ForPath("/jf/System/Info/Public")))
            .Build();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await container.StartAsync(deadline.Token);
        client = new HttpClient
        {
            BaseAddress = new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(8096), "/jf/").Uri,
            Timeout = TimeSpan.FromSeconds(30)
        };
        await AuthenticateAsync();
        var generated = await container.ExecAsync(
            command:
            [
                "/usr/lib/jellyfin-ffmpeg/ffmpeg", "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24",
                "-t", "120", "-vf", "setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709", "-c:v", "libx264", "-preset", "ultrafast",
                "-an", "-y", "/media/Reference (2026)/Reference (2026).mkv"
            ],
            ct: deadline.Token
        );
        if (generated.ExitCode != 0)
        {
            throw new InvalidOperationException("Synthetic video generation failed.");
        }

        await JsonAsync(
            path: "Library/VirtualFolders?name=Reference&collectionType=movies&paths=%2Fmedia&refreshLibrary=true",
            body: new
            {
                LibraryOptions = new
                {
                    EnableRealtimeMonitor = false,
                    EnableInternetProviders = false,
                    SaveLocalMetadata = false,
                    EnableChapterImageExtraction = false,
                    EnableTrickplayImageExtraction = false,
                    TypeOptions = new[]
                    {
                        new
                        {
                            Type = "Movie",
                            MetadataFetchers = Array.Empty<string>(),
                            ImageFetchers = Array.Empty<string>()
                        }
                    }
                }
            }
        );
        await UntilAsync(async () =>
        {
            var items = (await JsonAsync($"Items?userId={UserId}&Recursive=true&IncludeItemTypes=Movie"))?["Items"]?.AsArray();
            MovieId = items?.FirstOrDefault()?["Id"]?.GetValue<string>() ?? "";
            return MovieId.Length > 0;
        });
    }

    internal async Task<JsonNode?> JsonAsync(string path, object? body = null)
    {
        using var request = Request(body is null ? HttpMethod.Get : HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(
                inputValue: body,
                options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                }
            );
        }

        using var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? null : JsonNode.Parse(text);
    }

    internal async Task<HttpResponseMessage> PlaybackAsync(bool hdr, int maxBitrate)
    {
        using var request = Request(HttpMethod.Post, $"Items/{MovieId}/PlaybackInfo");
        if (hdr)
        {
            request.Headers.Add("X-Jellyfin-Hdr-Conversion", "true");
        }

        request.Content = JsonContent.Create(
            inputValue: new
            {
                UserId,
                IsPlayback = true,
                EnableDirectPlay = false,
                EnableDirectStream = false,
                AllowVideoStreamCopy = false,
                EnableTranscoding = true,
                MaxStreamingBitrate = maxBitrate,
                StartTimeTicks = 10000000,
                DeviceProfile = new
                {
                    MaxStreamingBitrate = maxBitrate,
                    DirectPlayProfiles = new[]
                    {
                        new
                        {
                            Type = "Video",
                            Container = "mkv,mp4",
                            VideoCodec = "h264,hevc",
                            AudioCodec = "aac"
                        }
                    },
                    TranscodingProfiles = new[]
                    {
                        new
                        {
                            Type = "Video",
                            Context = "Streaming",
                            Container = "ts",
                            Protocol = "hls",
                            VideoCodec = "h264",
                            AudioCodec = "aac"
                        }
                    },
                    CodecProfiles = new[]
                    {
                        new
                        {
                            Type = "Video",
                            Codec = "hevc",
                            Conditions = new[]
                            {
                                new
                                {
                                    Condition = "EqualsAny",
                                    Property = "VideoRangeType",
                                    Value = "SDR|HDR10"
                                }
                            }
                        }
                    }
                }
            },
            options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            }
        );
        return await Client.SendAsync(request);
    }

    internal async Task<string> TextAsync(string path)
    {
        using var request = Request(HttpMethod.Get, path);
        using var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    internal async Task<string> PlayerMenuAssetAsync()
    {
        var result = await container!.ExecAsync(["sh", "-c", """grep -l 'id:"quality"' /jellyfin/jellyfin-web/*.js | head -n 1"""]);
        return Path.GetFileName(result.Stdout.Trim());
    }

    internal async Task<string> ErrorsAsync()
    {
        var logs = await container!.GetLogsAsync();
        return string.Join(
            separator: Environment.NewLine,
            values: logs.Stdout.Split('\n').Concat(logs.Stderr.Split('\n'))
                        .Where(line => line.Contains("Exception", StringComparison.Ordinal) || line.Contains("   at Jellyfin.Plugin.HdrPlayback", StringComparison.Ordinal))
        );
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (path.StartsWith("/jf/", StringComparison.Ordinal))
        {
            path = path[4..];
        }

        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.TryAddWithoutValidation(
            name: "Authorization",
            value: token is null
                        ? "MediaBrowser Client=\"HDR application tests\", Device=\"Disposable server\", DeviceId=\"hdr-tests\", Version=\"1\""
                        : $"MediaBrowser Token=\"{token}\""
        );
        return request;
    }

    private async Task AuthenticateAsync()
    {
        var info = await JsonAsync("System/Info/Public");
        ServerId = info!["Id"]!.GetValue<string>();
        if (info?["StartupWizardCompleted"]?.GetValue<bool>() != true)
        {
            await UntilAsync(async () =>
            {
                try
                {
                    await JsonAsync("Startup/User");
                    return true;
                }
                catch (HttpRequestException)
                {
                    return false;
                }
            });
            await JsonAsync(
                path: "Startup/User",
                body: new
                {
                    Name = "hdr-test-admin",
                    Password = "Disposable-HDR-test-only-123!"
                }
            );
            await JsonAsync(
                path: "Startup/Configuration",
                body: new
                {
                    ServerName = "Disposable HDR tests",
                    UICulture = "en-US",
                    MetadataCountryCode = "US",
                    PreferredMetadataLanguage = "en"
                }
            );
            await JsonAsync(
                path: "Startup/RemoteAccess",
                body: new
                {
                    EnableRemoteAccess = true,
                    EnableAutomaticPortMapping = false
                }
            );
            await JsonAsync(
                path: "Startup/Complete",
                body: new
                {
                }
            );
        }

        var authentication = await JsonAsync(
            path: "Users/AuthenticateByName",
            body: new
            {
                Username = "hdr-test-admin",
                Pw = "Disposable-HDR-test-only-123!"
            }
        );
        token = authentication!["AccessToken"]!.GetValue<string>();
        UserId = authentication["User"]!["Id"]!.GetValue<string>();
    }

    private static void InstallBundle(string configuration, string scope)
    {
        var input = Path.Combine(AppContext.BaseDirectory, "Plugins", scope);
        var metadata = JsonNode.Parse(File.ReadAllText(Path.Combine(input, "meta.json")))!;
        var folder = metadata["name"]!.GetValue<string>() + "_" + metadata["version"]!.GetValue<string>();
        var destination = Directory.CreateDirectory(Path.Combine(configuration, "plugins", folder)).FullName;
        foreach (var name in metadata["assemblies"]!.AsArray().Select(value => value!.GetValue<string>()).Append("meta.json"))
        {
            File.Copy(Path.Combine(input, name), Path.Combine(destination, name));
        }
    }

    private static async Task UntilAsync(Func<Task<bool>> probe)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (!await probe())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (container is not null)
        {
            await container.DisposeAsync();
        }

        await network.DisposeAsync();

        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
