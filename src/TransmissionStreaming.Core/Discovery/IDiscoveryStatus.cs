namespace TransmissionStreaming.Core.Discovery;

/// <summary>Shares discovery health without tying the application to a hosted worker.</summary>
public interface IDiscoveryStatus
{
    DiscoveryStatus Get();
    void Set(DiscoveryStatus status);
}
