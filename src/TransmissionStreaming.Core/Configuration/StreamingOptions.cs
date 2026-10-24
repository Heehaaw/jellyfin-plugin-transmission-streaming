namespace TransmissionStreaming.Core.Configuration;

/// <summary>An immutable snapshot of connection, projection, and playback settings.</summary>
public sealed record StreamingOptions
{
    public bool Enabled { get; init; }
    public string StateDirectory { get; init; } = "";
    public string TransmissionUrl { get; init; } = "http://127.0.0.1:9091/transmission/rpc";
    public string TransmissionUsername { get; init; } = "";
    public string TransmissionPassword { get; init; } = "";
    public string RadarrUrl { get; init; } = "";
    public string RadarrApiKey { get; init; } = "";
    public string SonarrUrl { get; init; } = "";
    public string SonarrApiKey { get; init; } = "";
    public string RemoteMediaRoot { get; init; } = "/data";
    public string LocalMediaRoot { get; init; } = "/mnt/media";
    public string ProjectionRoot { get; init; } = "";
    public string JellyfinPublicUrl { get; init; } = "http://127.0.0.1:8096";
    public int PollSeconds { get; init; } = 10;
    public int PieceWaitSeconds { get; init; } = 120;
    public bool PrioritizePlayback { get; init; } = true;
    public bool ShowDownloadStatus { get; init; } = true;

    // Records otherwise print credential-bearing properties when included in a log message.
    public override string ToString()
    {
        return "Transmission Streaming settings (credentials redacted)";
    }
}
