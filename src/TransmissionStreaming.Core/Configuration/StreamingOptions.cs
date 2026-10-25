namespace TransmissionStreaming.Core.Configuration;

/// <summary>Connection, projection, and playback settings rebuilt when the host configuration changes.</summary>
public sealed record StreamingOptions
{
    public bool Enabled { get; set; }

    public string StateDirectory { get; set; } = "";

    public string TransmissionUrl { get; set; } = "http://127.0.0.1:9091/transmission/rpc";

    public string TransmissionUsername { get; set; } = "";

    public string TransmissionPassword { get; set; } = "";

    public string RadarrUrl { get; set; } = "";

    public string RadarrApiKey { get; set; } = "";

    public string SonarrUrl { get; set; } = "";

    public string SonarrApiKey { get; set; } = "";

    public IReadOnlyList<ServicePathMapping> PathMappings { get; set; } = [];

    public string RemoteMediaRoot { get; set; } = "/data";

    public string LocalMediaRoot { get; set; } = "/mnt/media";

    public string ProjectionRoot { get; set; } = "";

    public string JellyfinPublicUrl { get; set; } = "http://127.0.0.1:8096";

    public int PollSeconds { get; set; } = 10;

    public int PieceWaitSeconds { get; set; } = 120;

    public bool PrioritizePlayback { get; set; } = true;

    public bool ShowDownloadStatus { get; set; } = true;

    // Records otherwise print credential-bearing properties when included in a log message.
    public override string ToString()
    {
        return "Transmission Streaming settings (credentials redacted)";
    }
}
