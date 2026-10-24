namespace TransmissionStreaming.Core.Streaming;

/// <summary>Tracks active reads without retaining their video data.</summary>
public interface IStreamActivity
{
    int Count(string id);
    IDisposable Open(string id);
}
