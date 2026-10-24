namespace TransmissionStreaming.Arr.Discovery;

using static TransmissionStreaming.Arr.Transport.ArrJson;

using global::TransmissionStreaming.Arr;
using global::TransmissionStreaming.Arr.Transport;
using System.Text.Json;


/// <summary>Maps queued torrent files to unambiguous arr media identities.</summary>
public sealed class ArrDownloadDiscovery(ArrTransport transport)
{
    /// <summary>Finds the exact movie or episode associated with a torrent in the arr queue.</summary>
    public async Task<MediaIdentity?> FindDownloadAsync(
        ArrConnection connection,
        MediaKind kind,
        string hash,
        string filename,
        CancellationToken cancellationToken)
    {
        if (!MediaFileName.IsPlayable(filename) || string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        var suffix = kind == MediaKind.Movie
            ? "includeMovie=true&includeUnknownMovieItems=false"
            : "includeSeries=true&includeEpisode=true&includeUnknownSeriesItems=false";
        var records = await transport.GetPagesAsync(connection, "queue", suffix, cancellationToken).ConfigureAwait(false);
        var matches = records.Where(row => string.Equals(Text(row, "downloadId"), hash, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        if (kind == MediaKind.Movie)
        {
            var ids = matches.Select(row => Number(row, "movieId")).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length != 1)
            {
                return null;
            }

            var row = matches.First(row => Number(row, "movieId") == ids[0]);
            var movie = Object(row, "movie");
            if (movie.ValueKind != JsonValueKind.Object)
            {
                movie = await transport.GetAsync(connection, $"movie/{ids[0]}", cancellationToken).ConfigureAwait(false);
            }

            var movieIdentity = Identity(kind, movie, Text(row, "downloadId"), filename);
            movieIdentity.MovieId = ids[0];
            return movieIdentity;
        }

        var episodeNumber = MediaFileName.ParseEpisode(filename);
        if (episodeNumber is null)
        {
            return null;
        }

        var seriesIds = matches.Select(row => Number(row, "seriesId")).Where(id => id > 0).Distinct().ToArray();
        if (seriesIds.Length != 1)
        {
            return null;
        }

        var seriesId = seriesIds[0];
        var episodes = await transport.GetAsync(connection, $"episode?seriesId={seriesId}&seasonNumber={episodeNumber.Value.Season}", cancellationToken).ConfigureAwait(false);
        var candidates = Array(episodes).Where(episode =>
            Number(episode, "seasonNumber") == episodeNumber.Value.Season &&
            Number(episode, "episodeNumber") == episodeNumber.Value.Episode).ToList();
        if (candidates.Count != 1)
        {
            return null;
        }

        var first = matches[0];
        var series = Object(first, "series");
        if (series.ValueKind != JsonValueKind.Object)
        {
            series = await transport.GetAsync(connection, $"series/{seriesId}", cancellationToken).ConfigureAwait(false);
        }

        var identity = Identity(kind, series, Text(first, "downloadId"), filename);
        identity.SeriesId = seriesId;
        identity.EpisodeId = Number(candidates[0], "id");
        identity.SeasonNumber = episodeNumber.Value.Season;
        identity.EpisodeNumber = episodeNumber.Value.Episode;
        identity.EpisodeTitle = Text(candidates[0], "title");
        return identity;
    }

    private static MediaIdentity Identity(MediaKind kind, JsonElement media, string hash, string filename) => new()
    {
        Kind = kind,
        Title = Text(media, "title"),
        Year = Number(media, "year"),
        TmdbId = Number(media, "tmdbId"),
        TvdbId = Number(media, "tvdbId"),
        ImdbId = Text(media, "imdbId"),
        OriginalFileName = filename,
        DownloadHash = hash
    };


}
