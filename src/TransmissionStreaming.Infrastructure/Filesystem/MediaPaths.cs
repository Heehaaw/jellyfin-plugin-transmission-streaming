namespace TransmissionStreaming.Infrastructure.Filesystem;

using Core.Projection;

using global::TransmissionStreaming.Core.Arr;
using System.Security.Cryptography;
using System.Text;

/// <summary>Stable display paths and confined remote-to-local media translation.</summary>
internal sealed class MediaPaths : IProjectionPaths
{
    public string Map(string remotePath, string remoteRoot, string localRoot)
    {
        var sourceRoot = Path.GetFullPath(remoteRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var source = Path.GetFullPath(remotePath);
        if (!source.StartsWith(sourceRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Media path is outside the configured remote root.");
        }

        var targetRoot = Path.GetFullPath(localRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(targetRoot, source[sourceRoot.Length..]));
        if (!target.StartsWith(targetRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Media path escapes the configured local root.");
        }
        // A local filesystem alias must not turn an allowed download into an arbitrary file read.
        var relative = Path.GetRelativePath(targetRoot, target);
        var cursor = targetRoot.TrimEnd(Path.DirectorySeparatorChar);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            cursor = Path.Combine(cursor, segment);
            if (new FileInfo(cursor).LinkTarget is not null)
            {
                throw new InvalidOperationException("Symbolic links are not supported in media paths.");
            }
        }
        return target;
    }

    public string IdentityKey(MediaIdentity identity)
    {
        return identity.Kind == MediaKind.Movie
        ? $"movie:{identity.MovieId}"
        : $"episode:{identity.SeriesId}:{identity.SeasonNumber}:{identity.EpisodeNumber}";
    }

    public string Id(MediaIdentity identity)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(IdentityKey(identity))))[..32];
    }

    public string StreamPath(string root, MediaIdentity identity)
    {
        var title = SafeName(identity.Title);
        var folder = identity.Year > 0 ? $"{title} ({identity.Year})" : title;
        return identity.Kind == MediaKind.Movie
            ? Path.Combine(root, "movies", folder, folder + ".strm")
            : Path.Combine(root, "shows", folder, $"Season {identity.SeasonNumber:00}", $"{title} - S{identity.SeasonNumber:00}E{identity.EpisodeNumber:00}.strm");
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var name = new string(value.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
    }
}
