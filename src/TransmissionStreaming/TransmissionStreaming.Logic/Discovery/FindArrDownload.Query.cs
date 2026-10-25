namespace TransmissionStreaming.Logic.Discovery;

using Core.Arr;
using Mediator;

/// <summary>Resolves conservative movie or single-episode ownership from arr facts.</summary>
public static class FindArrDownload
{
    /// <summary>Requests ownership of one eligible torrent-relative file.</summary>
    public sealed record Query(ArrConnection Connection, MediaKind Kind, string Hash, string Filename) : IQuery<MediaIdentity?>;

    internal sealed class Handler(IArrClient arr) : IQueryHandler<Query, MediaIdentity?>
    {
        public async ValueTask<MediaIdentity?> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MediaFileName.IsPlayable(query.Filename) || string.IsNullOrWhiteSpace(query.Hash))
            {
                return null;
            }

            var queue = await arr.GetQueueAsync(query.Connection, query.Kind, cancellationToken).ConfigureAwait(false);
            var matches = queue.Where(row => row.DownloadHash.Equals(query.Hash, StringComparison.OrdinalIgnoreCase)).ToArray();
            var ids = matches.Select(row => query.Kind == MediaKind.Movie ? row.MovieId : row.SeriesId).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length != 1)
            {
                return null;
            }

            var id = ids[0];
            ArrEpisode? episode = null;
            if (query.Kind == MediaKind.Episode)
            {
                var number = MediaFileName.ParseEpisode(query.Filename);
                if (number is null)
                {
                    return null;
                }

                var episodes = await arr.GetEpisodesAsync(query.Connection, id, number.Value.Season, null, cancellationToken).ConfigureAwait(false);
                var candidates = episodes.Where(row => row.Season == number.Value.Season && row.Number == number.Value.Episode).ToArray();
                if (candidates.Length != 1)
                {
                    return null;
                }

                episode = candidates[0];
            }

            var match = query.Kind == MediaKind.Movie ? matches.First(row => row.MovieId == id) : matches[0];
            var media = match.Media ?? await arr.GetMediaAsync(query.Connection, query.Kind, id, cancellationToken).ConfigureAwait(false);
            return media with
            {
                MovieId = query.Kind == MediaKind.Movie ? id : 0,
                SeriesId = query.Kind == MediaKind.Episode ? id : 0,
                OriginalFileName = query.Filename,
                DownloadHash = match.DownloadHash,
                EpisodeId = episode?.Id ?? 0,
                SeasonNumber = episode?.Season ?? 0,
                EpisodeNumber = episode?.Number ?? 0,
                EpisodeTitle = episode?.Title ?? string.Empty
            };
        }
    }
}
