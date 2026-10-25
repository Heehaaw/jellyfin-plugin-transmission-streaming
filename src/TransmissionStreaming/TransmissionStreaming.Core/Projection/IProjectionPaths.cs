namespace TransmissionStreaming.Core.Projection;

using Arr;

/// <summary>Confined media path translation and stable identity naming.</summary>
public interface IProjectionPaths
{
    /// <summary>Translates a remote media path into the local root without moving or creating files.</summary>
    /// <exception cref="InvalidOperationException">The path escapes a configured root or traverses a symbolic link beneath the local root.</exception>
    string Map(string remotePath, string remoteRoot, string localRoot);

    /// <summary>Builds the movie or episode identity key independently of its title, download hash, and current file location.</summary>
    string IdentityKey(MediaIdentity identity);

    /// <summary>Derives the permanent projection identifier from its arr media identity.</summary>
    string Id(MediaIdentity identity);

    /// <summary>Builds the projection's movie or episode stream-file path without creating it.</summary>
    string StreamPath(string root, MediaIdentity identity);
}
