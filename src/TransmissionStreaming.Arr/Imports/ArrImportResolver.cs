namespace TransmissionStreaming.Arr.Imports;

using static TransmissionStreaming.Arr.Transport.ArrJson;

using global::TransmissionStreaming.Arr;
using global::TransmissionStreaming.Arr.Transport;
using System.Globalization;
using System.Text.Json;


/// <summary>Confirms that an imported library file belongs to the original torrent file.</summary>
public sealed class ArrImportResolver(ArrTransport transport)
{
    /// <summary>Resolves a completed file only after the same torrent's import is proven by history.</summary>
    public async Task<string?> FindImportedFileAsync(
        ArrConnection connection,
        MediaIdentity identity,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        if (expectedSize <= 0 || string.IsNullOrWhiteSpace(identity.DownloadHash))
        {
            return null;
        }

        var records = await transport.GetPagesAsync(connection, "history",
            "eventType=3&sortKey=date&sortDirection=descending&downloadId=" + Uri.EscapeDataString(identity.DownloadHash),
            cancellationToken).ConfigureAwait(false);

        // A matching title or size is insufficient: upgrades can leave an older file in the library.
        var imports = records.Where(row =>
            IsImport(row) &&
            string.Equals(Text(row, "downloadId"), identity.DownloadHash, StringComparison.OrdinalIgnoreCase) &&
            MatchesIdentity(row, identity) &&
            string.Equals(Basename(Data(row, "droppedPath")), Basename(identity.OriginalFileName), StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(Data(row, "size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size == expectedSize &&
            int.TryParse(Data(row, "fileId"), out var fileId) && fileId > 0)
            .ToList();
        var paths = imports.Select(row => Data(row, "importedPath")).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToList();
        if (paths.Count != 1)
        {
            return null;
        }

        var fileIds = imports.Select(row => int.Parse(Data(row, "fileId"), CultureInfo.InvariantCulture)).Distinct().ToList();
        if (fileIds.Count != 1)
        {
            return null;
        }

        var endpoint = identity.Kind == MediaKind.Movie ? "moviefile" : "episodefile";
        JsonElement file;
        try
        {
            file = await transport.GetAsync(connection, $"{endpoint}/{fileIds[0]}", cancellationToken).ConfigureAwait(false);
        }
        catch (ArrNotFoundException)
        {
            return null;
        }

        if (Long(file, "size") != expectedSize || !string.Equals(Text(file, "path"), paths[0], StringComparison.Ordinal))
        {
            return null;
        }

        var fileMediaId = Number(file, identity.Kind == MediaKind.Movie ? "movieId" : "seriesId");
        if (fileMediaId != (identity.Kind == MediaKind.Movie ? identity.MovieId : identity.SeriesId))
        {
            return null;
        }

        if (identity.Kind == MediaKind.Episode)
        {
            var episodes = await transport.GetAsync(connection, $"episode?seriesId={identity.SeriesId}&episodeFileId={fileIds[0]}", cancellationToken).ConfigureAwait(false);
            // Sonarr prefers seriesId over episodeFileId, so filter the returned episodes ourselves.
            var linked = Array(episodes).Where(row => Number(row, "episodeFileId") == fileIds[0]).ToList();
            if (linked.Count != 1 || Number(linked[0], "id") != identity.EpisodeId)
            {
                return null;
            }
        }

        return paths[0];
    }

    private static bool MatchesIdentity(JsonElement row, MediaIdentity identity) => identity.Kind == MediaKind.Movie
        ? Number(row, "movieId") == identity.MovieId
        : Number(row, "seriesId") == identity.SeriesId && Number(row, "episodeId") == identity.EpisodeId;

    private static bool IsImport(JsonElement row) => Text(row, "eventType").Equals("downloadFolderImported", StringComparison.OrdinalIgnoreCase) || Number(row, "eventType") == 3;


}
