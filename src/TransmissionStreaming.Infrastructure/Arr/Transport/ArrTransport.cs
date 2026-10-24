namespace TransmissionStreaming.Infrastructure.Arr.Transport;

using global::TransmissionStreaming.Core.Arr;
using System.Net;
using System.Text.Json;
using static TransmissionStreaming.Infrastructure.Arr.Transport.ArrJson;

/// <summary>Performs read-only authenticated arr API requests and bounded pagination.</summary>
/// <remarks>The caller owns the HTTP client and must disable automatic redirects to protect API keys.</remarks>
internal sealed class ArrTransport(HttpClient http) : IArrTransport
{
    private const int PageSize = 100;

    public async Task<List<JsonElement>> GetPagesAsync(ArrConnection connection, string endpoint, string query, CancellationToken cancellationToken)
    {
        var result = new List<JsonElement>();
        for (var page = 1; page <= 1000; page++)
        {
            var response = await GetAsync(connection, $"{endpoint}?page={page}&pageSize={PageSize}&{query}", cancellationToken).ConfigureAwait(false);
            var rows = Array(Object(response, "records")).ToArray();
            result.AddRange(rows);
            if (rows.Length == 0 || result.Count >= Number(response, "totalRecords"))
            {
                return result;
            }
        }

        throw new InvalidDataException("Arr pagination did not terminate.");
    }

    public async Task<JsonElement> GetAsync(ArrConnection connection, string path, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(connection.Url.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new ArgumentException("Arr URL must be an HTTP(S) base URL without credentials, query, or fragment.", nameof(connection));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "api/v3/" + path));
        request.Headers.Add("X-Api-Key", connection.ApiKey);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ArrNotFoundException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Arr request failed with HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.Clone();
    }

}

internal sealed class ArrNotFoundException : Exception
{
}
