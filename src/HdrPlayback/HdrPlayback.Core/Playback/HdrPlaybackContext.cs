namespace HdrPlayback.Core.Playback;

/// <summary>Host-neutral facts used to select optional SDR-to-HDR processing.</summary>
public sealed record HdrPlaybackContext(bool Requested, bool SourceIsSdr, bool ClientSupportsHdr, bool CanTranscode, bool PipelineSupported);

/// <summary>Describes whether conversion is available and selected, with a user-facing unavailable reason.</summary>
public sealed record HdrPlaybackDecision(bool Available, bool Enabled, string? Reason);