namespace TransmissionStreaming.Core.Streaming;

using Transmission;
using ProjectionEntry = Projection.Projection;

/// <summary>Request-local playback data shared across refreshes of one progressive copy.</summary>
/// <remarks>Contains no services or callbacks. A new instance belongs to each copy command, so concurrent readers have independent caches.</remarks>
public sealed record PlaybackSession(ProjectionEntry Entry)
{
    /// <summary>The most recently available torrent metadata, retained when the torrent is later removed.</summary>
    public TorrentSnapshot? Torrent
    {
        get;
        set;
    }

    /// <summary>The session settings returned alongside the cached torrent metadata.</summary>
    public TransmissionSessionSnapshot? Transmission
    {
        get;
        set;
    }

    /// <summary>The monotonic timestamp of the last upstream refresh, or null before the first lookup.</summary>
    public long? RefreshedAt
    {
        get;
        set;
    }

    /// <summary>The monotonic timestamp of the last persisted import lookup, or null before the first lookup.</summary>
    public long? CompletionCheckedAt
    {
        get;
        set;
    }

    /// <summary>The imported backing path after validation against the service's local mapping.</summary>
    public string? ImportedPath
    {
        get;
        set;
    }

    public override string ToString()
    {
        return "Playback session (stream details redacted)";
    }
}
