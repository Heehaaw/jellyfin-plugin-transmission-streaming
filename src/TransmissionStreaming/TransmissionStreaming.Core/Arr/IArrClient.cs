namespace TransmissionStreaming.Core.Arr;

/// <summary>Reads typed Radarr and Sonarr facts without deciding ownership or import acceptance.</summary>
public interface IArrClient
{
    /// <summary>Reads configured library roots in the upstream filesystem namespace.</summary>
    Task<IReadOnlyList<string>> GetRootFoldersAsync(ArrConnection connection, CancellationToken cancellationToken);

    /// <summary>Reads every queue page, retaining embedded media metadata when supplied.</summary>
    Task<IReadOnlyList<ArrQueueEntry>> GetQueueAsync(ArrConnection connection, MediaKind kind, CancellationToken cancellationToken);

    /// <summary>Reads one movie or series document.</summary>
    Task<MediaIdentity> GetMediaAsync(ArrConnection connection, MediaKind kind, int id, CancellationToken cancellationToken);

    /// <summary>Reads episodes using upstream season or file filters. Callers must validate returned membership.</summary>
    Task<IReadOnlyList<ArrEpisode>> GetEpisodesAsync(ArrConnection connection, int seriesId, int? season, int? fileId, CancellationToken cancellationToken);

    /// <summary>Reads import history pages for a download, preserving the identity and file evidence.</summary>
    Task<IReadOnlyList<ArrImport>> GetImportsAsync(ArrConnection connection, string hash, CancellationToken cancellationToken);

    /// <summary>Reads a current library file, returning null only when that file endpoint returns 404.</summary>
    Task<ArrMediaFile?> GetFileAsync(ArrConnection connection, MediaKind kind, int id, CancellationToken cancellationToken);

    /// <summary>Reads the validated movie or series library. Failed or malformed collections propagate.</summary>
    Task<IReadOnlyList<MediaIdentity>> GetLibraryAsync(ArrConnection connection, MediaKind kind, CancellationToken cancellationToken);
}
