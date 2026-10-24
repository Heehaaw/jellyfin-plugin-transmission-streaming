namespace TransmissionStreaming.Infrastructure.Arr.Transport;

using Core.Arr;
using System.Text.Json;

/// <summary>Read-only arr transport used by discovery and import adapters.</summary>
internal interface IArrTransport
{
    Task<List<JsonElement>> GetPagesAsync(ArrConnection connection, string endpoint, string query, CancellationToken cancellationToken);
    Task<JsonElement> GetAsync(ArrConnection connection, string path, CancellationToken cancellationToken);
}
