namespace TransmissionStreaming.Core.Discovery;

/// <summary>Credential-free status of the most recent discovery attempt.</summary>
public sealed record DiscoveryStatus
{
    /// <summary>True while a discovery attempt is still running.</summary>
    public bool IsPolling { get; init; }

    public string? LastFailure { get; init; }

    public DateTimeOffset? LastSuccessfulPoll { get; init; }
}
