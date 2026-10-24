namespace TransmissionStreaming.Core.Arr;

/// <summary>Connection details for one Radarr or Sonarr instance.</summary>
public sealed record ArrConnection
{
    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString()
    {
        return "ArrConnection { Credentials = [redacted] }";
    }
}
