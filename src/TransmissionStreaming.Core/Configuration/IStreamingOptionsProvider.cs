namespace TransmissionStreaming.Core.Configuration;

/// <summary>Supplies a consistent, fresh configuration snapshot independently of the plugin host.</summary>
public interface IStreamingOptionsProvider
{
    /// <summary>Captures the current settings for one operation; later calls may observe configuration changes.</summary>
    /// <remarks>The snapshot contains upstream credentials and must not be serialized into diagnostics.</remarks>
    StreamingOptions Get();
}
