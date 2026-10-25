namespace TransmissionStreaming.Infrastructure.Arr;

using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr.Discovery;
using global::TransmissionStreaming.Infrastructure.Arr.Imports;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Text.Json;

/// <summary>Read-only download discovery and verified import resolution for Radarr and Sonarr v3.</summary>
internal sealed class ArrClient(IArrDownloadDiscovery discovery, IArrImportResolver imports, IArrTransport transport) : IArrClient
{
    /// <summary>Reads configured roots without assuming they share the downloader's mount layout.</summary>
    public async Task<IReadOnlyList<string>> GetRootFoldersAsync(ArrConnection connection, CancellationToken cancellationToken)
    {
        var response = await transport.GetAsync(connection, "rootfolder", cancellationToken).ConfigureAwait(false);
        return ArrJson.Array(response).Select(folder => ArrJson.Text(folder, "path"))
            .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<bool> IsMediaTrackedAsync(ArrConnection connection, MediaIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Kind is not (MediaKind.Movie or MediaKind.Episode))
        {
            return true;
        }

        var id = identity.Kind == MediaKind.Movie ? identity.MovieId : identity.SeriesId;
        var imdb = PositiveImdbId(identity.ImdbId);
        if (id <= 0 && identity.TmdbId <= 0 && identity.TvdbId <= 0 && imdb is null)
        {
            return true;
        }

        var endpoint = identity.Kind == MediaKind.Movie ? "movie" : "series";
        var response = await transport.GetAsync(connection, endpoint, cancellationToken).ConfigureAwait(false);
        if (response.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Arr library lookup must return a collection.");
        }

        foreach (var row in response.EnumerateArray())
        {
            // A partial or unexpected document cannot prove that an existing library item was removed.
            if (row.ValueKind != JsonValueKind.Object || ArrJson.Number(row, "id") <= 0)
            {
                throw new JsonException("Arr library collection contains an invalid media entry.");
            }

            if ((id > 0 && ArrJson.Number(row, "id") == id)
                || (identity.TmdbId > 0 && ArrJson.Number(row, "tmdbId") == identity.TmdbId)
                || (identity.TvdbId > 0 && ArrJson.Number(row, "tvdbId") == identity.TvdbId)
                || (imdb is not null && string.Equals(PositiveImdbId(ArrJson.Text(row, "imdbId")), imdb, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string? PositiveImdbId(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: > 2 } && trimmed.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
                                          && trimmed.AsSpan(2).IndexOfAnyExceptInRange('0', '9') < 0 && trimmed.AsSpan(2).ContainsAnyExcept('0')
            ? trimmed
            : null;
    }

    /// <summary>Finds the exact movie or episode associated with a torrent in the arr queue.</summary>
    public Task<MediaIdentity?> FindDownloadAsync(
        ArrConnection connection,
        MediaKind kind,
        string hash,
        string filename,
        CancellationToken cancellationToken
    )
    {
        return discovery.FindDownloadAsync(connection, kind, hash, filename, cancellationToken);
    }

    /// <summary>Resolves a completed file only after the same torrent's import is proven by history.</summary>
    public Task<string?> FindImportedFileAsync(
        ArrConnection connection,
        MediaIdentity identity,
        long expectedSize,
        CancellationToken cancellationToken
    )
    {
        return imports.FindImportedFileAsync(connection, identity, expectedSize, cancellationToken);
    }
}
