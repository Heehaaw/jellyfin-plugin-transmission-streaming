namespace TransmissionStreaming.Core.Configuration;

/// <summary>Reads remote directories without changing service settings or translating them into local paths.</summary>
public interface IServicePathDiscovery
{
    /// <summary>Returns paths visible to the selected service; authentication and transport failures propagate to the caller.</summary>
    Task<DiscoveredServicePaths> DiscoverAsync(MediaService service, ServiceConnection connection, CancellationToken cancellationToken);
}
