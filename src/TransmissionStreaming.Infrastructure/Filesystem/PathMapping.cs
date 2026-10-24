namespace TransmissionStreaming.Infrastructure.Filesystem;

/// <summary>Maps a Transmission directory onto a locally readable media mount.</summary>
internal sealed record PathMapping(string RemoteRoot, string LocalRoot)
{
    /// <summary>Maps a path, refusing traversal and symlink escapes from the configured media root.</summary>
    public string? Map(string remotePath)
    {
        var remote = RemoteRoot.TrimEnd('/');
        if (remote.Length == 0 || !remote.StartsWith('/'))
        {
            return null;
        }

        if (remotePath != remote && !remotePath.StartsWith(remote + "/", StringComparison.Ordinal))
        {
            return null;
        }

        var relative = remotePath[remote.Length..].TrimStart('/');
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or ".." || part.Contains('\\') || part.Contains('\0')))
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(LocalRoot) || LocalRoot.TrimEnd('/').Length == 0)
        {
            return null;
        }

        var root = Path.GetFullPath(LocalRoot).TrimEnd(Path.DirectorySeparatorChar);
        var local = Path.GetFullPath(Path.Combine([root, .. parts]));
        if (local != root && !local.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }
        // The configured root itself may be a mount; symlinks beneath it must not escape that mount.
        var walk = root;
        foreach (var part in parts)
        {
            walk = Path.Combine(walk, part);
            if (new FileInfo(walk).LinkTarget is not null || new DirectoryInfo(walk).LinkTarget is not null)
            {
                return null;
            }
        }
        return local;
    }
}
