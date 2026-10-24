namespace TransmissionStreaming.Infrastructure.Transmission;

using Core.Transmission;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

/// <summary>Small Transmission 4.1 JSON-RPC client. The supplied HTTP handler must disable redirects.</summary>
internal sealed class TransmissionClient(HttpClient httpClient) : ITransmissionClient
{
    private const string SessionHeader = "X-Transmission-Session-Id";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private string? _sessionId;

    private static readonly string[] TorrentFields =
    [
        "hash_string", "name", "download_dir", "piece_count", "piece_size", "pieces", "files",
        "percent_done", "status", "error_string", "sequential_download", "sequential_download_from_piece"
    ];

    /// <summary>Reads torrent metadata without changing ownership or seeding settings.</summary>
    public Task<IReadOnlyList<TorrentSnapshot>> GetTorrentsAsync(TransmissionConnection connection, CancellationToken cancellationToken = default)
    {
        return ReadTorrentsAsync(connection, null, cancellationToken);
    }

    /// <summary>Reads one torrent's piece map without repeatedly transferring maps for unrelated downloads.</summary>
    public async Task<TorrentSnapshot?> GetTorrentAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default)
    {
        var torrents = await ReadTorrentsAsync(connection, hash, cancellationToken).ConfigureAwait(false);
        return torrents.FirstOrDefault(t => t.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<TorrentSnapshot>> ReadTorrentsAsync(TransmissionConnection connection, string? hash, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object> { ["fields"] = TorrentFields };
        if (hash is not null)
        {
            parameters["ids"] = new[] { hash };
        }

        var result = await CallAsync(connection, "torrent_get", parameters, cancellationToken).ConfigureAwait(false);
        return result.GetProperty("torrents").Deserialize<List<TorrentSnapshot>>(Json) ?? [];
    }

    /// <summary>Reads download location and write-cache settings.</summary>
    public async Task<TransmissionSessionSnapshot> GetSessionAsync(TransmissionConnection connection, CancellationToken cancellationToken = default)
    {
        var result = await CallAsync(connection, "session_get", new
        {
            fields = new[] { "incomplete_dir", "incomplete_dir_enabled", "rename_partial_files", "cache_size_mib", "version" }
        }, cancellationToken).ConfigureAwait(false);
        return result.Deserialize<TransmissionSessionSnapshot>(Json) ?? throw new IOException("Transmission returned an empty session.");
    }

    /// <summary>Moves an already-sequential torrent's cursor, without changing its wanted files or seeding policy.</summary>
    public async Task SetSequentialStartAsync(TransmissionConnection connection, string hash, int piece, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(piece);
        await CallAsync(connection, "torrent_set", new { ids = new[] { hash }, sequential_download_from_piece = piece }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Enables sequential disk-safe writes for an explicitly selected torrent.</summary>
    public async Task EnableSequentialAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default)
    {
        await CallAsync(connection, "torrent_set", new { ids = new[] { hash }, sequential_download = true }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement> CallAsync(TransmissionConnection connection, string method, object parameters, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(connection.Url, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException("Transmission URL must be an HTTP(S) endpoint without embedded credentials.", nameof(connection));
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = JsonContent.Create(new { jsonrpc = "2.0", method, @params = parameters, id = 1 });
            if (!string.IsNullOrEmpty(connection.Username))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(connection.Username + ":" + connection.Password)));
            }

            var session = Volatile.Read(ref _sessionId);
            if (session is not null)
            {
                request.Headers.TryAddWithoutValidation(SessionHeader, session);
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict && response.Headers.TryGetValues(SessionHeader, out var values))
            {
                Volatile.Write(ref _sessionId, values.First());
                continue;
            }
            // Redirects must not forward RPC credentials to a different host.
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                throw new IOException("Transmission RPC redirects are not supported.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new IOException($"Transmission RPC failed with HTTP {(int)response.StatusCode}.");
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out _))
            {
                throw new IOException("Transmission rejected the RPC request.");
            }

            if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            {
                throw new IOException("Transmission returned an invalid JSON-RPC response.");
            }

            return result.Clone();
        }
        throw new IOException("Transmission session negotiation did not complete.");
    }
}
