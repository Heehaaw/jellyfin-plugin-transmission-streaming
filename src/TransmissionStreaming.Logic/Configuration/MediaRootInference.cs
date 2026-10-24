namespace TransmissionStreaming.Logic.Configuration;

/// <summary>Finds a common parent while excluding the plugin's own generated library.</summary>
internal sealed class MediaRootInference : IMediaRootInference
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public string? Infer(IReadOnlyList<string> libraryPaths, string projectionRoot)
    {
        var ownedRoot = Normalize(projectionRoot);
        if (ownedRoot is null)
        {
            return null;
        }

        string? common = null;
        foreach (var libraryPath in libraryPaths)
        {
            var folder = Normalize(libraryPath);
            if (folder is null || IsWithin(folder, ownedRoot))
            {
                continue;
            }

            var parent = Path.GetDirectoryName(folder);
            if (parent is null)
            {
                continue;
            }
            common ??= parent;
            while (!IsWithin(parent, common))
            {
                common = Path.GetDirectoryName(common);
                if (common is null)
                {
                    return null;
                }
            }
        }

        // A filesystem root is too broad to identify the user's shared media mount.
        return common is null || string.Equals(common, Path.GetPathRoot(common), PathComparison) ? null : common;
    }

    private static string? Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return null;
        }
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsWithin(string path, string root)
    {
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return string.Equals(path, root, PathComparison) || path.StartsWith(prefix, PathComparison);
    }
}
