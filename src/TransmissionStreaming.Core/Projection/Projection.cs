namespace TransmissionStreaming.Core.Projection;

using global::TransmissionStreaming.Core.Arr;

/// <summary>A managed Jellyfin stream entry retained until cancellation or successful native handoff.</summary>
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
    public bool HandoffPending { get; set; }
    public MediaIdentity Identity { get; set; } = new();
    public string Status { get; set; } = "Preparing stream";
    public double Progress { get; set; }

    public override string ToString()
    {
        return $"Projection {{ Id = {Id}, Status = {Status} }}";
    }
}
