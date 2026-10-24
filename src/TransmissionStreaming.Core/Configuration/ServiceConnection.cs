namespace TransmissionStreaming.Core.Configuration;

/// <summary>Connection details supplied for one read-only service discovery request.</summary>
/// <param name="Url">The service's own endpoint.</param>
/// <param name="Username">The Transmission RPC username when required.</param>
/// <param name="Password">The Transmission RPC password when required.</param>
/// <param name="ApiKey">The Radarr or Sonarr API key.</param>
public sealed record ServiceConnection(string Url, string Username = "", string Password = "", string ApiKey = "")
{
    /// <summary>Omits endpoints and credentials from diagnostics.</summary>
    public override string ToString()
    {
        return "Service connection (credentials redacted)";
    }
}
