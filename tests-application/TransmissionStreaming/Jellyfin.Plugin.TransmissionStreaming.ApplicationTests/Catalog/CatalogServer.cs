namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using global::TransmissionStreaming.TestSupport.Torrents;
using Hosting;

/// <summary>Serves this scenario's repository through the assembly-owned C# HTTP process.</summary>
internal sealed class CatalogServer(DownloadServices services, TorrentScenario scenario, bool corruptChecksum, Version releaseVersion)
{
    public PluginBundle Bundle { get; private set; } = PluginBundle.BuildOutput;

    private string Root => Path.Combine(scenario.Workspace, "catalog");
    public Uri ManifestUrl => new UriBuilder("http", SwarmSettings.CatalogHost, SwarmSettings.HttpPort, "/manifest.json").Uri;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var packageUrl = new UriBuilder("http", SwarmSettings.CatalogHost, SwarmSettings.HttpPort, "/package.zip").Uri;
        var version = await CatalogPackage.WriteAsync(Root, packageUrl, corruptChecksum, releaseVersion, cancellationToken);
        Bundle = PluginBundle.BuildOutput with
        {
            Version = version
        };
        await services.ControlAsync(
            operation: "catalog",
            value: new
            {
                directory = scenario.RemotePath(Root)
            },
            cancellationToken: cancellationToken
        );
    }

    public Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        return services.LogsAsync(cancellationToken);
    }
}
