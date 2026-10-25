namespace TransmissionStreaming.Logic.Discovery;

using Core.Arr;
using Mediator;

/// <summary>Preserves unknown identities and matches tracked media independently of the download queue.</summary>
public static class IsArrMediaTracked
{
    /// <summary>Checks current upstream ownership, including titles re-added with a different local ID.</summary>
    public sealed record Query(ArrConnection Connection, MediaIdentity Identity) : IQuery<bool>;

    internal sealed class Handler(IArrClient arr) : IQueryHandler<Query, bool>
    {
        public async ValueTask<bool> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = query.Identity;
            var id = identity.Kind == MediaKind.Movie ? identity.MovieId : identity.SeriesId;
            var imdb = PositiveImdbId(identity.ImdbId);
            if (identity.Kind is not (MediaKind.Movie or MediaKind.Episode)
                || id <= 0 && identity.TmdbId <= 0 && identity.TvdbId <= 0 && imdb is null)
            {
                return true;
            }

            var library = await arr.GetLibraryAsync(query.Connection, identity.Kind, cancellationToken).ConfigureAwait(false);
            return library.Any(row => (id > 0 && (identity.Kind == MediaKind.Movie ? row.MovieId : row.SeriesId) == id)
                || identity.TmdbId > 0 && row.TmdbId == identity.TmdbId
                || identity.TvdbId > 0 && row.TvdbId == identity.TvdbId
                || imdb is not null && string.Equals(PositiveImdbId(row.ImdbId), imdb, StringComparison.OrdinalIgnoreCase));
        }

        private static string? PositiveImdbId(string? value)
        {
            var trimmed = value?.Trim();
            return trimmed is { Length: > 2 } && trimmed.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
                && trimmed.AsSpan(2).IndexOfAnyExceptInRange('0', '9') < 0 && trimmed.AsSpan(2).ContainsAnyExcept('0') ? trimmed : null;
        }
    }
}
