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
    private const int MaximumSessionAttempts = 3;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    private async Task<IReadOnlyList<TorrentSnapshot>> ReadTorrentsAsync(TransmissionConnection connection, string? hash, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object>
        {
            ["fields"] = TorrentFields
        };
        if (hash is not null)
        {
            parameters["ids"] = new[]
            {
                hash
            };
        }

        var result = await CallAsync(connection, "torrent_get", parameters, cancellationToken).ConfigureAwait(false);
        return result.GetProperty("torrents").Deserialize<List<TorrentSnapshot>>(Json) ?? [];
    }

    private async Task<JsonElement> CallAsync(TransmissionConnection connection, string method, object parameters, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(connection.Url, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException("Transmission URL must be an HTTP(S) endpoint without embedded credentials.", nameof(connection));
        }

        for (var attempt = 0; attempt < MaximumSessionAttempts; attempt++)
        {
            // Each negotiation attempt owns its request and response, including an early retry.
            using var request = CreateRequest(connection, endpoint, method, parameters);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (TryUpdateSession(response))
            {
                continue;
            }

            EnsureSuccessfulResponse(response);
            return await ReadResultAsync(response, cancellationToken).ConfigureAwait(false);
        }

        throw new IOException("Transmission session negotiation did not complete.");
    }

    private HttpRequestMessage CreateRequest(TransmissionConnection connection, Uri endpoint, string method, object parameters)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters,
            id = 1
        };
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        if (!string.IsNullOrEmpty(connection.Username))
        {
            var credentials = connection.Username + ":" + connection.Password;
            var credentialBytes = Encoding.UTF8.GetBytes(credentials);
            var encodedCredentials = Convert.ToBase64String(credentialBytes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
        }

        var session = Volatile.Read(ref _sessionId);
        if (session is not null)
        {
            request.Headers.TryAddWithoutValidation(SessionHeader, session);
        }

        return request;
    }

    private bool TryUpdateSession(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Conflict || !response.Headers.TryGetValues(SessionHeader, out var values))
        {
            return false;
        }

        var session = values.First();
        Volatile.Write(ref _sessionId, session);
        return true;
    }

    private static void EnsureSuccessfulResponse(HttpResponseMessage response)
    {
        // Redirects must not forward RPC credentials to a different host.
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new IOException("Transmission RPC redirects are not supported.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new IOException($"Transmission RPC failed with HTTP {(int)response.StatusCode}.");
        }
    }

    private static async Task<JsonElement> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    /// <summary>Reads one torrent's piece map without repeatedly transferring maps for unrelated downloads.</summary>
    public async Task<TorrentSnapshot?> GetTorrentAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default)
    {
        var torrents = await ReadTorrentsAsync(connection, hash, cancellationToken).ConfigureAwait(false);
        return torrents.FirstOrDefault(torrent => torrent.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads download location and write-cache settings.</summary>
    public async Task<TransmissionSessionSnapshot> GetSessionAsync(TransmissionConnection connection, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            fields = new[]
            {
                "download_dir", "incomplete_dir", "incomplete_dir_enabled", "rename_partial_files", "cache_size_mib", "version"
            }
        };
        var result = await CallAsync(connection, "session_get", parameters, cancellationToken).ConfigureAwait(false);
        return result.Deserialize<TransmissionSessionSnapshot>(Json) ?? throw new IOException("Transmission returned an empty session.");
    }

    /// <summary>Moves an already-sequential torrent's cursor, without changing its wanted files or seeding policy.</summary>
    public async Task SetSequentialStartAsync(TransmissionConnection connection, string hash, int piece, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(piece);
        var parameters = new
        {
            ids = new[]
            {
                hash
            },
            sequential_download_from_piece = piece
        };
        await CallAsync(connection, "torrent_set", parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Enables sequential disk-safe writes for an explicitly selected torrent.</summary>
    public async Task EnableSequentialAsync(TransmissionConnection connection, string hash, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            ids = new[]
            {
                hash
            },
            sequential_download = true
        };
        await CallAsync(connection, "torrent_set", parameters, cancellationToken).ConfigureAwait(false);
    }
}
