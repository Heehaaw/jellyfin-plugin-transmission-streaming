namespace TransmissionStreaming.Core.Filesystem;

/// <summary>Translates one root pair while enforcing the local filesystem boundary.</summary>
public interface IMediaPathMapper
{
    /// <summary>Returns the confined local path, or null for invalid paths, traversal, and symlink escapes.</summary>
    string? Map(string path, string remoteRoot, string localRoot);
}
