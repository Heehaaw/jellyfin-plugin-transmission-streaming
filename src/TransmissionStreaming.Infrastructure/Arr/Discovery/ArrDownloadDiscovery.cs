namespace TransmissionStreaming.Infrastructure.Arr.Discovery;

using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Text.Json;
using static TransmissionStreaming.Infrastructure.Arr.Transport.ArrJson;

/// <summary>Maps queued torrent files to unambiguous arr media identities.</summary>
internal sealed class ArrDownloadDiscovery(IArrTransport transport) : IArrDownloadDiscovery
{
    /// <summary>Finds the exact movie or episode associated with a torrent in the arr queue.</summary>
    public async Task<MediaIdentity?> FindDownloadAsync(
        ArrConnection connection,
        MediaKind kind,
        string hash,
        string filename,
        CancellationToken cancellationToken
    )
    {
        if (!MediaFileName.IsPlayable(filename) || string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        var suffix = kind == MediaKind.Movie
            ? "includeMovie=true&includeUnknownMovieItems=false"
            : "includeSeries=true&includeEpisode=true&includeUnknownSeriesItems=false";
        var records = await transport.GetPagesAsync(connection, "queue", suffix, cancellationToken).ConfigureAwait(false);
        var matches = MatchingDownloads(records, hash);
        if (matches.Count == 0)
        {
            return null;
        }

        if (kind == MediaKind.Movie)
        {
            return await FindMovieAsync(connection, matches, filename, cancellationToken).ConfigureAwait(false);
        }

        return await FindEpisodeAsync(connection, kind, matches, filename, cancellationToken).ConfigureAwait(false);
    }

    private static List<JsonElement> MatchingDownloads(IReadOnlyList<JsonElement> records, string hash)
    {
        var matches = new List<JsonElement>();
        foreach (var row in records)
        {
            var downloadId = Text(row, "downloadId");
            var matchesHash = string.Equals(downloadId, hash, StringComparison.OrdinalIgnoreCase);
            if (matchesHash)
            {
                matches.Add(row);
            }
        }

        return matches;
    }

    private async Task<MediaIdentity?> FindMovieAsync(
        ArrConnection connection,
        IReadOnlyList<JsonElement> matches,
        string filename,
        CancellationToken cancellationToken
    )
    {
        var movieId = UniquePositiveId(matches, "movieId");
        if (movieId is null)
        {
            return null;
        }

        var row = matches.First(candidate =>
            {
                var candidateId = Number(candidate, "movieId");
                return candidateId == movieId.Value;
            }
        );
        var movie = Object(row, "movie");
        if (movie.ValueKind != JsonValueKind.Object)
        {
            movie = await transport.GetAsync(connection, $"movie/{movieId.Value}", cancellationToken).ConfigureAwait(false);
        }

        var hash = Text(row, "downloadId");
        var identity = Identity(MediaKind.Movie, movie, hash, filename);
        identity.MovieId = movieId.Value;
        return identity;
    }

    private static int? UniquePositiveId(IReadOnlyList<JsonElement> rows, string name)
    {
        var ids = new HashSet<int>();
        foreach (var row in rows)
        {
            var id = Number(row, name);
            if (id > 0)
            {
                ids.Add(id);
            }
        }

        return ids.Count == 1 ? ids.Single() : null;
    }

    private static MediaIdentity Identity(MediaKind kind, JsonElement media, string hash, string filename)
    {
        return new()
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

    private async Task<MediaIdentity?> FindEpisodeAsync(
        ArrConnection connection,
        MediaKind kind,
        IReadOnlyList<JsonElement> matches,
        string filename,
        CancellationToken cancellationToken
    )
    {
        var episodeNumber = MediaFileName.ParseEpisode(filename);
        if (episodeNumber is null)
        {
            return null;
        }

        var seriesId = UniquePositiveId(matches, "seriesId");
        if (seriesId is null)
        {
            return null;
        }

        var episode = await FindEpisodeRowAsync(connection, seriesId.Value, episodeNumber.Value, cancellationToken).ConfigureAwait(false);
        if (episode is null)
        {
            return null;
        }

        var first = matches[0];
        var series = await ReadSeriesAsync(connection, first, seriesId.Value, cancellationToken).ConfigureAwait(false);
        var hash = Text(first, "downloadId");
        var identity = Identity(kind, series, hash, filename);
        identity.SeriesId = seriesId.Value;
        identity.EpisodeId = Number(episode.Value, "id");
        identity.SeasonNumber = episodeNumber.Value.Season;
        identity.EpisodeNumber = episodeNumber.Value.Episode;
        identity.EpisodeTitle = Text(episode.Value, "title");
        return identity;
    }

    private async Task<JsonElement?> FindEpisodeRowAsync(
        ArrConnection connection,
        int seriesId,
        EpisodeNumber number,
        CancellationToken cancellationToken
    )
    {
        var episodes = await transport.GetAsync(connection, $"episode?seriesId={seriesId}&seasonNumber={number.Season}", cancellationToken).ConfigureAwait(false);
        var candidates = new List<JsonElement>();
        foreach (var episode in Array(episodes))
        {
            var season = Number(episode, "seasonNumber");
            if (season != number.Season)
            {
                continue;
            }

            var episodeNumber = Number(episode, "episodeNumber");
            if (episodeNumber == number.Episode)
            {
                candidates.Add(episode);
            }
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private async Task<JsonElement> ReadSeriesAsync(
        ArrConnection connection,
        JsonElement row,
        int seriesId,
        CancellationToken cancellationToken
    )
    {
        var series = Object(row, "series");
        if (series.ValueKind == JsonValueKind.Object)
        {
            return series;
        }

        return await transport.GetAsync(connection, $"series/{seriesId}", cancellationToken).ConfigureAwait(false);
    }
}
