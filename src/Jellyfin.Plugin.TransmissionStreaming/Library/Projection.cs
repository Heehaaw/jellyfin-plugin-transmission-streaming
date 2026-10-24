namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Arr;


/// <summary>A permanent Jellyfin stream entry whose backing file may move.</summary>
public sealed record Projection
{
    public string Id { get; set; } = "";
    public string Secret { get; set; } = "";
    public string TorrentHash { get; set; } = "";
    public int FileIndex { get; set; }
    public string SourceName { get; set; } = "";
    public long Length { get; set; }
    public string StreamPath { get; set; } = "";
    public string? CompletedPath { get; set; }
    public MediaIdentity Identity { get; set; } = new();
    public string Status { get; set; } = "Preparing stream";
    public double Progress { get; set; }
}
