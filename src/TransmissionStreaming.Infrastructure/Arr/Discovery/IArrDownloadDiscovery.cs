namespace TransmissionStreaming.Infrastructure.Arr.Discovery;

using Core.Arr;

/// <summary>Finds an unambiguous media identity in the download queue.</summary>
internal interface IArrDownloadDiscovery
{
    Task<MediaIdentity?> FindDownloadAsync(ArrConnection connection, MediaKind kind, string hash, string filename, CancellationToken cancellationToken);
}
