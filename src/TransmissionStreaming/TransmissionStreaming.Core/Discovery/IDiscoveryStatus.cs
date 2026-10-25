namespace TransmissionStreaming.Core.Discovery;

/// <summary>Shares discovery health without tying the application to a hosted worker.</summary>
public interface IDiscoveryStatus
{
    /// <summary>Returns the latest discovery health snapshot for status reporting.</summary>
    DiscoveryStatus Get();

    /// <summary>Replaces the health snapshot after a discovery poll succeeds or fails.</summary>
    /// <remarks>Failure details must exclude credentials, stream secrets, and credential-bearing upstream URLs.</remarks>
    void Set(DiscoveryStatus status);
}
