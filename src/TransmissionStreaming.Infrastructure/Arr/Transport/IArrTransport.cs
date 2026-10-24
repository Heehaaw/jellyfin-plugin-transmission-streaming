namespace TransmissionStreaming.Infrastructure.Arr.Transport;

using Core.Arr;
using System.Text.Json;

/// <summary>Read-only arr transport used by discovery and import adapters.</summary>
internal interface IArrTransport
{
    /// <summary>Reads every page of an arr collection and returns independent JSON records after responses are disposed.</summary>
    Task<List<JsonElement>> GetPagesAsync(ArrConnection connection, string endpoint, string query, CancellationToken cancellationToken);
    /// <summary>Reads one arr resource and returns JSON whose lifetime is independent of the HTTP response.</summary>
    Task<JsonElement> GetAsync(ArrConnection connection, string path, CancellationToken cancellationToken);
}
