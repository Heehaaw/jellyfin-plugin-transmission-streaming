namespace TransmissionStreaming.Infrastructure.Discovery;

using Core.Discovery;

/// <summary>Publishes immutable discovery status safely across worker and request threads.</summary>
internal sealed class DiscoveryStatusStore : IDiscoveryStatus
{
    private DiscoveryStatus status = new();

    public DiscoveryStatus Get()
    {
        return Volatile.Read(ref status);
    }

    public void Set(DiscoveryStatus value)
    {
        Volatile.Write(ref status, value);
    }
}
