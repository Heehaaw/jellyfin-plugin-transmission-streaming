namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;

/// <summary>Merges a duplicate's per-user history into the permanent Jellyfin entry before reconciliation.</summary>
internal sealed class WatchStateMerger : IWatchStateMerger
{
    /// <summary>Preserves the best-known playback state and independently retains personal metadata from either entry.</summary>
    /// <remarks>
    /// A missing timestamp cannot establish whether a manual watched or resume change is older.
    /// In that conflict the permanent target wins. A completely untouched duplicate never clears it.
    /// Play counts use the maximum because both entries may describe the same playback session.
    /// </remarks>
    public UpdateUserItemDataDto Merge(UserItemData source, UserItemData? target)
    {
        ArgumentNullException.ThrowIfNull(source);
        var playback = SelectPlayback(source, target);

        return new UpdateUserItemDataDto
        {
            PlaybackPositionTicks = playback.PlaybackPositionTicks,
            Played = playback.Played,
            LastPlayedDate = playback.LastPlayedDate,
            PlayCount = Math.Max(source.PlayCount, target?.PlayCount ?? 0),
            IsFavorite = source.IsFavorite || (target?.IsFavorite ?? false),
            Rating = target?.Rating ?? source.Rating,
            Likes = target?.Likes ?? source.Likes
        };
    }

    private static UserItemData SelectPlayback(UserItemData source, UserItemData? target)
    {
        if (target is null || !HasActivity(target))
        {
            return source;
        }

        if (!HasActivity(source))
        {
            return target;
        }

        // A timestamped playback can replace another timestamped playback, but cannot
        // prove itself newer than a date-less manual change on the permanent entry.
        return source.LastPlayedDate is { } sourceAt
               && target.LastPlayedDate is { } targetAt
               && sourceAt > targetAt
            ? source
            : target;
    }

    private static bool HasActivity(UserItemData data)
    {
        return data.LastPlayedDate.HasValue
        || data.Played || data.PlayCount > 0 || data.PlaybackPositionTicks > 0;
    }
}
