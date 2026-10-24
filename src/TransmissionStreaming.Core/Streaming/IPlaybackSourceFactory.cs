namespace TransmissionStreaming.Core.Streaming;

/// <summary>Creates an isolated source for each playback request.</summary>
public interface IPlaybackSourceFactory
{
    IProgressiveSource Create(Projection.Projection entry);
}
