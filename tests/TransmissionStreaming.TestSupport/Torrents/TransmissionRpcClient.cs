namespace TransmissionStreaming.TestSupport.Torrents;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>Negotiates the disposable daemon's JSON-RPC session without involving production adapters.</summary>
internal sealed class TransmissionRpcClient(HttpClient http, Func<Uri> endpoint)
{
    private const string SessionHeader = "X-Transmission-Session-Id";
    private const int MaximumSessionAttempts = 3;
    private string? sessionId;

    public async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumSessionAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint())
            {
                Content = JsonContent.Create(new { jsonrpc = "2.0", method, @params = parameters, id = 1 })
            };
            if (sessionId is not null)
            {
                request.Headers.Add(SessionHeader, sessionId);
            }

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                sessionId = response.Headers.GetValues(SessionHeader).Single();
                continue;
            }

            response.EnsureSuccessStatusCode();
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (body.RootElement.TryGetProperty("error", out _))
            {
                throw new IOException("The disposable Transmission daemon rejected a fixture RPC request.");
            }

            return body.RootElement.GetProperty("result").Clone();
        }

        throw new IOException("The disposable daemon did not negotiate an RPC session.");
    }
}
