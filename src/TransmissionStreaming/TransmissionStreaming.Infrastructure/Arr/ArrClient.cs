namespace TransmissionStreaming.Infrastructure.Arr;

using Core.Arr;
using Transport;
using System.Globalization;
using System.Text.Json;
using static Transport.ArrJson;

/// <summary>Maps read-only Radarr and Sonarr v3 responses to host-neutral facts.</summary>
internal sealed class ArrClient(IArrTransport transport) : IArrClient
{
    public async Task<IReadOnlyList<string>> GetRootFoldersAsync(ArrConnection connection, CancellationToken cancellationToken)
    {
        var response = await transport.GetAsync(connection, "rootfolder", cancellationToken).ConfigureAwait(false);
        return Array(response).Select(folder => Text(folder, "path")).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<ArrQueueEntry>> GetQueueAsync(ArrConnection connection, MediaKind kind, CancellationToken cancellationToken)
    {
        var suffix = kind == MediaKind.Movie ? "includeMovie=true&includeUnknownMovieItems=false" : "includeSeries=true&includeEpisode=true&includeUnknownSeriesItems=false";
        var rows = await transport.GetPagesAsync(connection, "queue", suffix, cancellationToken).ConfigureAwait(false);
        return rows.Select(row =>
            {
                var media = Object(row, kind == MediaKind.Movie ? "movie" : "series");
                return new ArrQueueEntry(
                    DownloadHash: Text(row, "downloadId"),
                    MovieId: Number(row, "movieId"),
                    SeriesId: Number(row, "seriesId"),
                    Media: media.ValueKind == JsonValueKind.Object ? ReadMedia(media, kind) : null
                );
            }
        ).ToArray();
    }

    public async Task<MediaIdentity> GetMediaAsync(ArrConnection connection, MediaKind kind, int id, CancellationToken cancellationToken)
    {
        var endpoint = kind == MediaKind.Movie ? "movie" : "series";
        var row = await transport.GetAsync(connection, $"{endpoint}/{id}", cancellationToken).ConfigureAwait(false);
        return ReadMedia(row, kind);
    }

    public async Task<IReadOnlyList<ArrEpisode>> GetEpisodesAsync(ArrConnection connection, int seriesId, int? season, int? fileId, CancellationToken cancellationToken)
    {
        var suffix = season is not null ? $"&seasonNumber={season}" : $"&episodeFileId={fileId}";
        var rows = await transport.GetAsync(connection, $"episode?seriesId={seriesId}{suffix}", cancellationToken).ConfigureAwait(false);
        return Array(rows).Select(row => new ArrEpisode(
                Id: Number(row, "id"),
                Season: Number(row, "seasonNumber"),
                Number: Number(row, "episodeNumber"),
                FileId: Number(row, "episodeFileId"),
                Title: Text(row, "title")
            )
        ).ToArray();
    }

    public async Task<IReadOnlyList<ArrImport>> GetImportsAsync(ArrConnection connection, string hash, CancellationToken cancellationToken)
    {
        var query = "eventType=3&sortKey=date&sortDirection=descending&downloadId=" + Uri.EscapeDataString(hash);
        var rows = await transport.GetPagesAsync(connection, "history", query, cancellationToken).ConfigureAwait(false);
        return rows.Select(row =>
            {
                var isImport = Text(row, "eventType").Equals("downloadFolderImported", StringComparison.OrdinalIgnoreCase) || Number(row, "eventType") == 3;
                var parsedSize = long.TryParse(Data(row, "size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
                var parsedId = int.TryParse(Data(row, "fileId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId);
                return new ArrImport(
                    IsImport: isImport,
                    DownloadHash: Text(row, "downloadId"),
                    MovieId: Number(row, "movieId"),
                    SeriesId: Number(row, "seriesId"),
                    EpisodeId: Number(row, "episodeId"),
                    DroppedPath: Data(row, "droppedPath"),
                    ImportedPath: Data(row, "importedPath"),
                    Size: parsedSize ? size : null,
                    FileId: parsedId ? fileId : 0
                );
            }
        ).ToArray();
    }

    public async Task<ArrMediaFile?> GetFileAsync(ArrConnection connection, MediaKind kind, int id, CancellationToken cancellationToken)
    {
        var endpoint = kind == MediaKind.Movie ? "moviefile" : "episodefile";
        try
        {
            var row = await transport.GetAsync(connection, $"{endpoint}/{id}", cancellationToken).ConfigureAwait(false);
            return new ArrMediaFile(Text(row, "path"), Long(row, "size"), Number(row, "movieId"), Number(row, "seriesId"));
        }
        catch (ArrNotFoundException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<MediaIdentity>> GetLibraryAsync(ArrConnection connection, MediaKind kind, CancellationToken cancellationToken)
    {
        var endpoint = kind == MediaKind.Movie ? "movie" : "series";
        var rows = await transport.GetAsync(connection, endpoint, cancellationToken).ConfigureAwait(false);
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Arr library lookup must return a collection.");
        }

        var result = new List<MediaIdentity>();
        foreach (var row in rows.EnumerateArray())
        {
            // Partial library responses cannot prove that a title was deleted.
            if (row.ValueKind != JsonValueKind.Object || Number(row, "id") <= 0)
            {
                throw new JsonException("Arr library collection contains an invalid media entry.");
            }

            result.Add(ReadMedia(row, kind));
        }

        return result;
    }

    private static MediaIdentity ReadMedia(JsonElement row, MediaKind kind)
    {
        return new MediaIdentity
        {
            Kind = kind,
            MovieId = kind == MediaKind.Movie ? Number(row, "id") : 0,
            SeriesId = kind == MediaKind.Episode ? Number(row, "id") : 0,
            Title = Text(row, "title"),
            Year = Number(row, "year"),
            TmdbId = Number(row, "tmdbId"),
            TvdbId = Number(row, "tvdbId"),
            ImdbId = Text(row, "imdbId")
        };
    }
}
