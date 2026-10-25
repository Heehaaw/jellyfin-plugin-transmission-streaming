namespace HdrPlayback.Core.Playback;

/// <summary>Host-neutral conversion facts. A null client capability means preplay discovery, before a device profile is available.</summary>
public sealed record HdrPlaybackContext(bool Requested, bool SourceIsSdr, bool? ClientSupportsHdr, bool CanTranscode, bool PipelineSupported);

/// <summary>Describes whether conversion is available and selected, with a user-facing unavailable reason.</summary>
public sealed record HdrPlaybackDecision(bool Available, bool Enabled, string? Reason);
