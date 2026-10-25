namespace TransmissionStreaming.Logic.Library;

using Core.Projection;
using Mediator;

/// <summary>Merges playback and personal metadata without depending on host data types.</summary>
public static class MergeWatchState
{
    /// <summary>Requests preservation of the best-known source and target user data.</summary>
    public sealed record Query(LibraryWatchState Source, LibraryWatchState? Target) : IQuery<LibraryWatchState>;

    internal sealed class Handler : IQueryHandler<Query, LibraryWatchState>
    {
        public ValueTask<LibraryWatchState> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = query.Source;
            var target = query.Target;
            var playback = SelectPlayback(source, target);
            var merged = playback with
            {
                PlayCount = Math.Max(source.PlayCount, target?.PlayCount ?? 0),
                IsFavorite = source.IsFavorite || (target?.IsFavorite ?? false),
                Rating = target?.Rating ?? source.Rating,
                Likes = target?.Likes ?? source.Likes
            };
            return ValueTask.FromResult(merged);
        }

        private static LibraryWatchState SelectPlayback(LibraryWatchState source, LibraryWatchState? target)
        {
            if (target is null || !HasActivity(target))
            {
                return source;
            }

            if (!HasActivity(source))
            {
                return target;
            }

            // A timestamp cannot establish precedence over a date-less manual change on the target.
            return source.LastPlayedDate is { } sourceAt && target.LastPlayedDate is { } targetAt && sourceAt > targetAt ? source : target;
        }

        private static bool HasActivity(LibraryWatchState state)
        {
            return state.LastPlayedDate.HasValue
                   || state.Played || state.PlayCount > 0 || state.PlaybackPositionTicks > 0;
        }
    }
}
