namespace TransmissionStreaming.Core.Discovery;

/// <summary>Credential-free status of the most recent discovery attempt.</summary>
public sealed record DiscoveryStatus
{
    public string? LastFailure { get; init; }

    public DateTimeOffset? LastSuccessfulPoll { get; init; }
}
