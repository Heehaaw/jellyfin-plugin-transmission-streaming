namespace TransmissionStreaming.Core.Arr;

/// <summary>A queue row with its upstream identifiers and optional embedded movie or series.</summary>
public sealed record ArrQueueEntry(string DownloadHash, int MovieId, int SeriesId, MediaIdentity? Media);

/// <summary>Episode metadata and its current upstream file link.</summary>
public sealed record ArrEpisode(int Id, int Season, int Number, int FileId, string Title);

/// <summary>Parsed history evidence. Invalid size and file IDs remain absent or zero.</summary>
public sealed record ArrImport(
    bool IsImport,
    string DownloadHash,
    int MovieId,
    int SeriesId,
    int EpisodeId,
    string DroppedPath,
    string ImportedPath,
    long? Size,
    int FileId
);

/// <summary>A current upstream library file, before application acceptance.</summary>
public sealed record ArrMediaFile(string Path, long Size, int MovieId, int SeriesId);
