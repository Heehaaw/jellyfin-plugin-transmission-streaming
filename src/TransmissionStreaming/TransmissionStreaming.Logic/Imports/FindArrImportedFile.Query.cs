namespace TransmissionStreaming.Logic.Imports;

using Core.Arr;
using Mediator;

/// <summary>Accepts an import only when its history and current file prove ownership.</summary>
public static class FindArrImportedFile
{
    /// <summary>Requests import proof for the exact original download and byte length.</summary>
    public sealed record Query(ArrConnection Connection, MediaIdentity Identity, long ExpectedSize) : IQuery<string?>;

    internal sealed class Handler(IArrClient arr) : IQueryHandler<Query, string?>
    {
        public async ValueTask<string?> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = query.Identity;
            if (query.ExpectedSize <= 0 || string.IsNullOrWhiteSpace(identity.DownloadHash))
            {
                return null;
            }

            var history = await arr.GetImportsAsync(query.Connection, identity.DownloadHash, cancellationToken).ConfigureAwait(false);
            // Title or size alone cannot distinguish an upgrade from an older library file.
            var imports = history.Where(row => Matches(row, identity, query.ExpectedSize)).ToArray();
            var paths = imports.Select(row => row.ImportedPath).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
            // Blank-path rows still participate in ambiguity checks for file identity.
            var ids = imports.Select(row => row.FileId).Distinct().ToArray();
            if (paths.Length != 1 || ids.Length != 1)
            {
                return null;
            }

            var file = await arr.GetFileAsync(query.Connection, identity.Kind, ids[0], cancellationToken).ConfigureAwait(false);
            if (file is null || file.Size != query.ExpectedSize || !file.Path.Equals(paths[0], StringComparison.Ordinal) ||
                (identity.Kind == MediaKind.Movie ? file.MovieId != identity.MovieId : file.SeriesId != identity.SeriesId))
            {
                return null;
            }

            if (identity.Kind == MediaKind.Episode)
            {
                var episodes = await arr.GetEpisodesAsync(query.Connection, identity.SeriesId, null, ids[0], cancellationToken).ConfigureAwait(false);
                // Sonarr can prefer seriesId over episodeFileId, so verify membership ourselves.
                var linked = episodes.Where(row => row.FileId == ids[0]).ToArray();
                if (linked.Length != 1 || linked[0].Id != identity.EpisodeId)
                {
                    return null;
                }
            }

            return paths[0];
        }

        private static bool Matches(ArrImport row, MediaIdentity identity, long size)
        {
            return row.IsImport && row.DownloadHash.Equals(identity.DownloadHash, StringComparison.OrdinalIgnoreCase) &&
                   (identity.Kind == MediaKind.Movie ? row.MovieId == identity.MovieId : row.SeriesId == identity.SeriesId && row.EpisodeId == identity.EpisodeId) &&
                   Basename(row.DroppedPath).Equals(Basename(identity.OriginalFileName), StringComparison.OrdinalIgnoreCase) && row.Size == size && row.FileId > 0;
        }

        private static string Basename(string path)
        {
            return Path.GetFileName(path.Replace('\\', '/'));
        }
    }
}
