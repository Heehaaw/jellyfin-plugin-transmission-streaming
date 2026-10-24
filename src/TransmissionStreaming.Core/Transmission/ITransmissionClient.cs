namespace TransmissionStreaming.Core.Transmission;

/// <summary>Transmission operations needed for discovery and progressive reads.</summary>
public interface ITransmissionClient
{
    /// <summary>Reads the current torrent inventory and piece availability without changing download state.</summary>
    Task<IReadOnlyList<TorrentSnapshot>> GetTorrentsAsync(TransmissionConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Reads one torrent by hash, returning null after it leaves the client.</summary>
    Task<TorrentSnapshot?> GetTorrentAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default);

    /// <summary>Reads incomplete-directory and write-cache settings needed to locate files and decide whether disk reads are safe.</summary>
    Task<TransmissionSessionSnapshot> GetSessionAsync(TransmissionConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Moves one torrent's sequential cursor to a zero-based piece index across the entire torrent.</summary>
    /// <remarks>This cursor is shared by concurrent viewers of files in the same torrent.</remarks>
    Task SetSequentialStartAsync(TransmissionConnection connection, string hash, int piece, CancellationToken cancellationToken = default);

    /// <summary>Enables sequential downloading for one torrent without changing global cache, seeding, or file-selection settings.</summary>
    Task EnableSequentialAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default);
}
