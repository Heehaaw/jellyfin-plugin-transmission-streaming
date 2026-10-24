namespace TransmissionStreaming.Core.Streaming;

/// <summary>Creates an isolated source for each playback request.</summary>
public interface IPlaybackSourceFactory
{
    /// <summary>Creates a request-local source that follows this projection from its torrent file to an exact imported file.</summary>
    /// <remarks>Availability caches belong to this source and are not shared between playback requests.</remarks>
    IProgressiveSource Create(Projection.Projection entry);
}
