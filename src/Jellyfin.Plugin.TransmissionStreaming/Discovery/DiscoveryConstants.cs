namespace Jellyfin.Plugin.TransmissionStreaming.Discovery;

/// <summary>Bounds how frequently the prototype scans external download state.</summary>
internal static class DiscoveryConstants
{
    public const int MinimumPollSeconds = 2;
    public const int MaximumPollSeconds = 300;
    public const int SecretBytes = 32;
    public const string OwnershipMarker = ".transmission-streaming";
}
