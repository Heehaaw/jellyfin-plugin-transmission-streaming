namespace TransmissionStreaming.Core.Configuration;

/// <summary>Supplies a consistent, fresh configuration snapshot independently of the plugin host.</summary>
public interface IStreamingOptionsProvider
{
    StreamingOptions Get();
}
