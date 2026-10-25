namespace TransmissionStreaming.Core.Projection;

/// <summary>Host item facts used by reconciliation and native handoff decisions.</summary>
public sealed record LibraryItem(string Id, string Name, string? Overview, bool IsPlaying);

/// <summary>Per-user playback and personal metadata independent of the media-server implementation.</summary>
public sealed record LibraryWatchState
{
    public long PlaybackPositionTicks { get; set; }
    public bool Played { get; set; }
    public DateTime? LastPlayedDate { get; set; }
    public int PlayCount { get; set; }
    public bool IsFavorite { get; set; }
    public double? Rating { get; set; }
    public bool? Likes { get; set; }
}

/// <summary>Hydrated per-user states from the source and destination host entries.</summary>
public sealed record LibraryUserState(string UserId, LibraryWatchState? Source, LibraryWatchState? Target);
