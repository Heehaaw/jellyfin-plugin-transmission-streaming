namespace HdrPlayback.Core.Playback;

/// <summary>Identifies one authenticated Jellyfin playback session.</summary>
public readonly record struct HdrSessionKey(Guid UserId, string DeviceId, string PlaySessionId);

/// <summary>Tracks HDR selection only for active native playback sessions.</summary>
public interface IHdrSessionStore
{
    /// <summary>Marks one native playback session for conversion.</summary>
    void Enable(HdrSessionKey key);
    /// <summary>Checks and refreshes an unexpired selection.</summary>
    bool Contains(HdrSessionKey key);
    /// <summary>Clears only the specified stopped session.</summary>
    void Remove(HdrSessionKey key);
}
