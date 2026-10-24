namespace TransmissionStreaming.Core.Transmission;

/// <summary>Transmission operations needed for discovery and progressive reads.</summary>
public interface ITransmissionClient
{
    Task<IReadOnlyList<TorrentSnapshot>> GetTorrentsAsync(TransmissionConnection connection, CancellationToken cancellationToken = default);
    Task<TorrentSnapshot?> GetTorrentAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default);
    Task<TransmissionSessionSnapshot> GetSessionAsync(TransmissionConnection connection, CancellationToken cancellationToken = default);
    Task SetSequentialStartAsync(TransmissionConnection connection, string hash, int piece, CancellationToken cancellationToken = default);
    Task EnableSequentialAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default);
}
