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
        var records = await transport.GetPagesAsync(
            connection: connection,
            endpoint: "queue",
            query: suffix,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        var matches = MatchingDownloads(
            records: records,
            hash: hash
        );
        if (matches.Count == 0)
        {
            return null;
        }

        if (kind == MediaKind.Movie)
        {
            return await FindMovieAsync(
                connection: connection,
                matches: matches,
                filename: filename,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        return await FindEpisodeAsync(
            connection: connection,
            kind: kind,
            matches: matches,
            filename: filename,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    private static List<JsonElement> MatchingDownloads(IReadOnlyList<JsonElement> records, string hash)
    {
        var matches = new List<JsonElement>();
        foreach (var row in records)
        {
            var downloadId = Text(
                element: row,
                name: "downloadId"
            );
            var matchesHash = string.Equals(
                a: downloadId,
                b: hash,
                comparisonType: StringComparison.OrdinalIgnoreCase
            );
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
        var movieId = UniquePositiveId(
            rows: matches,
            name: "movieId"
        );
        if (movieId is null)
        {
            return null;
        }

        var row = matches.First(candidate =>
            {
                var candidateId = Number(
                    element: candidate,
                    name: "movieId"
                );
                return candidateId == movieId.Value;
            }
        );
        var movie = Object(
            element: row,
            name: "movie"
        );
        if (movie.ValueKind != JsonValueKind.Object)
        {
            movie = await transport.GetAsync(
                connection: connection,
                path: $"movie/{movieId.Value}",
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        var hash = Text(
            element: row,
            name: "downloadId"
        );
        var identity = Identity(
            kind: MediaKind.Movie,
            media: movie,
            hash: hash,
            filename: filename
        );
        identity.MovieId = movieId.Value;
        return identity;
    }

    private static int? UniquePositiveId(IReadOnlyList<JsonElement> rows, string name)
    {
        var ids = new HashSet<int>();
        foreach (var row in rows)
        {
            var id = Number(
                element: row,
                name: name
            );
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
            Title = Text(
                element: media,
                name: "title"
            ),
            Year = Number(
                element: media,
                name: "year"
            ),
            TmdbId = Number(
                element: media,
                name: "tmdbId"
            ),
            TvdbId = Number(
                element: media,
                name: "tvdbId"
            ),
            ImdbId = Text(
                element: media,
                name: "imdbId"
            ),
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

        var seriesId = UniquePositiveId(
            rows: matches,
            name: "seriesId"
        );
        if (seriesId is null)
        {
            return null;
        }

        var episode = await FindEpisodeRowAsync(
            connection: connection,
            seriesId: seriesId.Value,
            number: episodeNumber.Value,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        if (episode is null)
        {
            return null;
        }

        var first = matches[0];
        var series = await ReadSeriesAsync(
            connection: connection,
            row: first,
            seriesId: seriesId.Value,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        var hash = Text(
            element: first,
            name: "downloadId"
        );
        var identity = Identity(
            kind: kind,
            media: series,
            hash: hash,
            filename: filename
        );
        identity.SeriesId = seriesId.Value;
        identity.EpisodeId = Number(
            element: episode.Value,
            name: "id"
        );
        identity.SeasonNumber = episodeNumber.Value.Season;
        identity.EpisodeNumber = episodeNumber.Value.Episode;
        identity.EpisodeTitle = Text(
            element: episode.Value,
            name: "title"
        );
        return identity;
    }

    private async Task<JsonElement?> FindEpisodeRowAsync(
        ArrConnection connection,
        int seriesId,
        EpisodeNumber number,
        CancellationToken cancellationToken
    )
    {
        var episodes = await transport.GetAsync(
            connection: connection,
            path: $"episode?seriesId={seriesId}&seasonNumber={number.Season}",
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        var candidates = new List<JsonElement>();
        foreach (var episode in Array(episodes))
        {
            var season = Number(
                element: episode,
                name: "seasonNumber"
            );
            if (season != number.Season)
            {
                continue;
            }

            var episodeNumber = Number(
                element: episode,
                name: "episodeNumber"
            );
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
        var series = Object(
            element: row,
            name: "series"
        );
        if (series.ValueKind == JsonValueKind.Object)
        {
            return series;
        }

        return await transport.GetAsync(
            connection: connection,
            path: $"series/{seriesId}",
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}
