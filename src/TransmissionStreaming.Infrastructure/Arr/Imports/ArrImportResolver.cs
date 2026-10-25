namespace TransmissionStreaming.Infrastructure.Arr.Imports;

using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Globalization;
using System.Text.Json;
using static TransmissionStreaming.Infrastructure.Arr.Transport.ArrJson;

/// <summary>Confirms that an imported library file belongs to the original torrent file.</summary>
internal sealed class ArrImportResolver(IArrTransport transport) : IArrImportResolver
{
    /// <summary>Resolves a completed file only after the same torrent's import is proven by history.</summary>
    public async Task<string?> FindImportedFileAsync(
        ArrConnection connection,
        MediaIdentity identity,
        long expectedSize,
        CancellationToken cancellationToken
    )
    {
        if (expectedSize <= 0 || string.IsNullOrWhiteSpace(identity.DownloadHash))
        {
            return null;
        }

        var query = "eventType=3&sortKey=date&sortDirection=descending&downloadId=" + Uri.EscapeDataString(identity.DownloadHash);
        var records = await transport.GetPagesAsync(
            connection: connection,
            endpoint: "history",
            query: query,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        var candidate = FindCandidate(
            records: records,
            identity: identity,
            expectedSize: expectedSize
        );
        if (candidate is null)
        {
            return null;
        }

        var file = await ReadImportedFileAsync(
            connection: connection,
            kind: identity.Kind,
            fileId: candidate.FileId,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        if (file is null)
        {
            return null;
        }

        var matchesFile = MatchesImportedFile(
            file: file.Value,
            identity: identity,
            candidate: candidate,
            expectedSize: expectedSize
        );
        if (!matchesFile)
        {
            return null;
        }

        if (identity.Kind != MediaKind.Episode)
        {
            return candidate.Path;
        }

        var singleEpisode = await HasSingleImportedEpisodeAsync(
            connection: connection,
            identity: identity,
            fileId: candidate.FileId,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        return singleEpisode ? candidate.Path : null;
    }

    private static ImportCandidate? FindCandidate(IReadOnlyList<JsonElement> records, MediaIdentity identity, long expectedSize)
    {
        // A matching title or size is insufficient: upgrades can leave an older file in the library.
        var imports = new List<JsonElement>();
        foreach (var row in records)
        {
            var matches = MatchesImport(
                row: row,
                identity: identity,
                expectedSize: expectedSize
            );
            if (matches)
            {
                imports.Add(row);
            }
        }

        var path = UniqueImportPath(imports);
        if (path is null)
        {
            return null;
        }

        // Keep all matching rows in the ID check, including rows with a blank imported path.
        var fileIds = imports.Select(ReadFileId).Distinct().ToList();
        if (fileIds.Count != 1)
        {
            return null;
        }

        return new ImportCandidate(
            Path: path,
            FileId: fileIds[0]
        );
    }

    private static bool MatchesImport(JsonElement row, MediaIdentity identity, long expectedSize)
    {
        if (!IsImport(row))
        {
            return false;
        }

        var hash = Text(
            element: row,
            name: "downloadId"
        );
        var matchesHash = string.Equals(
            a: hash,
            b: identity.DownloadHash,
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
        if (!matchesHash)
        {
            return false;
        }

        var matchesIdentity = MatchesIdentity(
            row: row,
            identity: identity
        );
        if (!matchesIdentity)
        {
            return false;
        }

        var droppedPath = Data(
            row: row,
            name: "droppedPath"
        );
        var droppedName = Basename(droppedPath);
        var originalName = Basename(identity.OriginalFileName);
        var matchesName = string.Equals(
            a: droppedName,
            b: originalName,
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
        if (!matchesName)
        {
            return false;
        }

        var importedSize = Data(
            row: row,
            name: "size"
        );
        var parsedSize = long.TryParse(
            s: importedSize,
            style: NumberStyles.Integer,
            provider: CultureInfo.InvariantCulture,
            result: out var size
        );
        if (!parsedSize || size != expectedSize)
        {
            return false;
        }

        var importedFileId = Data(
            row: row,
            name: "fileId"
        );
        var parsedFileId = int.TryParse(
            s: importedFileId,
            result: out var fileId
        );
        return parsedFileId && fileId > 0;
    }

    private static bool IsImport(JsonElement row)
    {
        var eventType = Text(
            element: row,
            name: "eventType"
        );
        var namedImport = eventType.Equals(
            value: "downloadFolderImported",
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
        if (namedImport)
        {
            return true;
        }

        var eventNumber = Number(
            element: row,
            name: "eventType"
        );
        return eventNumber == 3;
    }

    private static bool MatchesIdentity(JsonElement row, MediaIdentity identity)
    {
        if (identity.Kind == MediaKind.Movie)
        {
            var movieId = Number(
                element: row,
                name: "movieId"
            );
            return movieId == identity.MovieId;
        }

        var seriesId = Number(
            element: row,
            name: "seriesId"
        );
        if (seriesId != identity.SeriesId)
        {
            return false;
        }

        var episodeId = Number(
            element: row,
            name: "episodeId"
        );
        return episodeId == identity.EpisodeId;
    }

    private static string? UniqueImportPath(IReadOnlyList<JsonElement> imports)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in imports)
        {
            var path = Data(
                row: row,
                name: "importedPath"
            );
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths.Count == 1 ? paths.Single() : null;
    }

    private static int ReadFileId(JsonElement row)
    {
        var value = Data(
            row: row,
            name: "fileId"
        );
        return int.Parse(
            s: value,
            provider: CultureInfo.InvariantCulture
        );
    }

    private async Task<JsonElement?> ReadImportedFileAsync(ArrConnection connection, MediaKind kind, int fileId, CancellationToken cancellationToken)
    {
        var endpoint = kind == MediaKind.Movie ? "moviefile" : "episodefile";
        try
        {
            return await transport.GetAsync(
                connection: connection,
                path: $"{endpoint}/{fileId}",
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }
        catch (ArrNotFoundException)
        {
            return null;
        }
    }

    private static bool MatchesImportedFile(JsonElement file, MediaIdentity identity, ImportCandidate candidate, long expectedSize)
    {
        var size = Long(
            element: file,
            name: "size"
        );
        if (size != expectedSize)
        {
            return false;
        }

        var path = Text(
            element: file,
            name: "path"
        );
        var matchesPath = string.Equals(
            a: path,
            b: candidate.Path,
            comparisonType: StringComparison.Ordinal
        );
        if (!matchesPath)
        {
            return false;
        }

        var field = identity.Kind == MediaKind.Movie ? "movieId" : "seriesId";
        var fileMediaId = Number(
            element: file,
            name: field
        );
        var expectedMediaId = identity.Kind == MediaKind.Movie ? identity.MovieId : identity.SeriesId;
        return fileMediaId == expectedMediaId;
    }

    private async Task<bool> HasSingleImportedEpisodeAsync(ArrConnection connection, MediaIdentity identity, int fileId, CancellationToken cancellationToken)
    {
        var episodes = await transport.GetAsync(
            connection: connection,
            path: $"episode?seriesId={identity.SeriesId}&episodeFileId={fileId}",
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        // Sonarr prefers seriesId over episodeFileId, so filter the returned episodes ourselves.
        var linked = new List<JsonElement>();
        foreach (var row in Array(episodes))
        {
            var linkedFileId = Number(
                element: row,
                name: "episodeFileId"
            );
            if (linkedFileId == fileId)
            {
                linked.Add(row);
            }
        }

        if (linked.Count != 1)
        {
            return false;
        }

        var episodeId = Number(
            element: linked[0],
            name: "id"
        );
        return episodeId == identity.EpisodeId;
    }

    private sealed record ImportCandidate(string Path, int FileId);
}
