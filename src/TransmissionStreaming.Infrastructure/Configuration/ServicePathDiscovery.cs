namespace TransmissionStreaming.Infrastructure.Configuration;

using Core.Arr;
using Core.Configuration;
using Core.Transmission;

/// <summary>Reads each service independently because their Docker mounts need not match.</summary>
internal sealed class ServicePathDiscovery(ITransmissionClient transmission, IArrClient arr) : IServicePathDiscovery
{
    public async Task<DiscoveredServicePaths> DiscoverAsync(MediaService service, ServiceConnection connection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (service)
        {
            case MediaService.Transmission:
                var rpc = new TransmissionConnection(connection.Url, connection.Username, connection.Password);
                var session = await transmission.GetSessionAsync(rpc, cancellationToken).ConfigureAwait(false);
                var torrents = await transmission.GetTorrentsAsync(rpc, cancellationToken).ConfigureAwait(false);
                var directories = torrents.Select(torrent => torrent.DownloadDirectory).Prepend(session.DownloadDirectory)
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
                return new DiscoveredServicePaths
                {
                    DownloadDirectory = Present(session.DownloadDirectory),
                    IncompleteDirectory = session.IncompleteDirectoryEnabled ? Present(session.IncompleteDirectory) : null,
                    DownloadDirectories = directories
                };
            case MediaService.Radarr:
            case MediaService.Sonarr:
                var roots = await arr.GetRootFoldersAsync(
                    connection: new ArrConnection
                    {
                        Url = connection.Url,
                        ApiKey = connection.ApiKey
                    },
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
                return new DiscoveredServicePaths
                {
                    LibraryRoots = roots
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(service), service, "Unsupported media service.");
        }
    }

    private static string? Present(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
