namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using global::TransmissionStreaming.TestSupport;

/// <summary>Packages the built plugin and owns one fresh Jellyfin process and its persistent configuration.</summary>
internal sealed class JellyfinContainer : IAsyncDisposable
{
    public const int HttpPort = 8096;
    public const string NetworkAlias = "jellyfin";
    public static readonly TimeSpan MediaDuration = TimeSpan.FromSeconds(12);
    public static readonly Uri InternalAddress = new($"http://{NetworkAlias}:{HttpPort}");
    private const string Image = "jellyfin/jellyfin:12.3@sha256:2038fe038ab3876a80f6e7b7e0aef6607b032c7dc0c9b3ad3296d0b2eba3bbb5";
    private const string MediaFileName = "reference.mkv";
    private readonly string workspace;
    private readonly IContainer container;

    public JellyfinContainer(string workspace, INetwork network, bool installPlugin)
    {
        this.workspace = workspace;
        var configuration = Path.Combine(workspace, "jellyfin");
        PrepareConfiguration(configuration, installPlugin);
        var cache = Directory.CreateDirectory(Path.Combine(workspace, "cache")).FullName;
        container = new ContainerBuilder(Image)
            .WithCreateParameterModifier(parameters => parameters.User = ContainerIdentity.User)
            .WithNetwork(network)
            .WithNetworkAliases(NetworkAlias)
            .WithPortBinding(HttpPort, true)
            .WithBindMount(configuration, "/config")
            .WithBindMount(cache, "/cache")
            .WithBindMount(workspace, "/data")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/System/Info/Public")))
            .Build();
    }

    public Uri Address => new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(HttpPort)).Uri;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return container.StartAsync(cancellationToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await container.StopAsync(cancellationToken);
        await container.StartAsync(cancellationToken);
    }

    public async Task<byte[]> CreateVideoAsync(CancellationToken cancellationToken)
    {
        var remotePath = $"/data/{MediaFileName}";
        var generated = await container.ExecAsync([
            "/usr/lib/jellyfin-ffmpeg/ffmpeg", "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24",
            "-t", MediaDuration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:v", "libx264", "-preset", "ultrafast", "-an", "-y", remotePath], cancellationToken);
        if (generated.ExitCode != 0)
        {
            throw new InvalidOperationException("Could not generate synthetic test video.");
        }
        return await File.ReadAllBytesAsync(Path.Combine(workspace, MediaFileName), cancellationToken);
    }

    public async Task<string> LogsAsync()
    {
        var logs = await container.GetLogsAsync();
        return logs.Stdout + logs.Stderr;
    }

    public ValueTask DisposeAsync()
    {
        return container.DisposeAsync();
    }

    private static void PrepareConfiguration(string configuration, bool installPlugin)
    {
        if (installPlugin)
        {
            PluginBundle.BuildOutput.InstallTo(Path.Combine(configuration, "plugins"));
        }

        var settings = Directory.CreateDirectory(Path.Combine(configuration, "config")).FullName;
        File.WriteAllText(Path.Combine(settings, "network.xml"), """
            <NetworkConfiguration>
                <EnableAutoDiscovery>false</EnableAutoDiscovery>
                <EnableUPnP>false</EnableUPnP>
                <EnableIPv6>false</EnableIPv6>
            </NetworkConfiguration>
            """);
    }
}
