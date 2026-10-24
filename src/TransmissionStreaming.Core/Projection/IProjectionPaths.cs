namespace TransmissionStreaming.Core.Projection;

using Arr;

/// <summary>Confined media path translation and stable identity naming.</summary>
public interface IProjectionPaths
{
    string Map(string remotePath, string remoteRoot, string localRoot);
    string IdentityKey(MediaIdentity identity);
    string Id(MediaIdentity identity);
    string StreamPath(string root, MediaIdentity identity);
}
