namespace TransmissionStreaming.Core.Configuration;

/// <summary>An editable remote-to-local root mapping scoped to one service.</summary>
/// <remarks>Parameterless construction and writable properties support Jellyfin's XML configuration serializer.</remarks>
public sealed record ServicePathMapping
{
    /// <summary>The service whose reported paths use this mapping.</summary>
    public MediaService Service { get; set; }

    /// <summary>A directory as seen by that service.</summary>
    public string RemoteRoot { get; set; } = "";

    /// <summary>The matching directory as seen by Jellyfin.</summary>
    public string LocalRoot { get; set; } = "";
}
