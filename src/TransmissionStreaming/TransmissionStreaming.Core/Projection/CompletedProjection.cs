namespace TransmissionStreaming.Core.Projection;

/// <summary>A successful transition from a progressive projection to a native Jellyfin item.</summary>
/// <remarks>History retains no stream capability, credentials or filesystem paths.</remarks>
/// <param name="Id">The retired projection's permanent identifier.</param>
/// <param name="Title">The media title displayed in the completion history.</param>
/// <param name="NativeItemId">The native Jellyfin item used for subsequent playback.</param>
/// <param name="CompletedAt">When the native handoff finished successfully.</param>
public sealed record CompletedProjection(string Id, string Title, string NativeItemId, DateTimeOffset CompletedAt);
