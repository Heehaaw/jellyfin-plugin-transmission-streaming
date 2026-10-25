namespace HdrPlayback.Core.Transcoding;

/// <summary>Adds HDR processing to an existing FFmpeg video-transcode command, preserving native input and quality arguments.</summary>
public interface IHdrCommandTransformer
{
    /// <summary>Adds conversion to a supported native command while retaining its input and playback limits.</summary>
    string Transform(string arguments);
}
