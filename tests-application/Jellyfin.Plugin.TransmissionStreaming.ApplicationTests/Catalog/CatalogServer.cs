namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using global::TransmissionStreaming.TestSupport.Torrents;

/// <summary>Serves this scenario's repository through the assembly-owned C# HTTP process.</summary>
internal sealed class CatalogServer(DownloadServices services, TorrentScenario scenario, bool corruptChecksum)
{
    private string Root => Path.Combine(scenario.Workspace, "catalog");
    public Uri ManifestUrl => new UriBuilder("http", SwarmSettings.CatalogHost, SwarmSettings.HttpPort, "/manifest.json").Uri;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var packageUrl = new UriBuilder("http", SwarmSettings.CatalogHost, SwarmSettings.HttpPort, "/package.zip").Uri;
        CatalogPackage.Write(Root, packageUrl, corruptChecksum);
        await services.ControlAsync("catalog", new { directory = scenario.RemotePath(Root) }, cancellationToken);
    }

    public Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        return services.LogsAsync(cancellationToken);
    }
}
