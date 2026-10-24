namespace Jellyfin.Plugin.TransmissionStreaming.Streaming;

/// <summary>Stable routes and playback limits shared by endpoints and configuration.</summary>
internal static class StreamingConstants
{
    public const string Route = "TransmissionStreaming";
    public const string RequiresAdministrator = "RequiresElevation";
    public const int MinimumStallSeconds = 5;
    public const int MaximumStallSeconds = 600;
    public static readonly TimeSpan CompletedPathRefresh = TimeSpan.FromMilliseconds(500);
}
