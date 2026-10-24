namespace TransmissionStreaming.Logic.Configuration;

using Mediator;

/// <summary>Finds a common media-library parent without inspecting files or upstream services.</summary>
public static class InferMediaRoot
{
    /// <summary>Requests a local media root while excluding the owned projection and filesystem roots.</summary>
    public sealed record Query(IReadOnlyList<string> LibraryPaths, string ProjectionRoot) : IQuery<string?>;

    internal sealed class Handler : IQueryHandler<Query, string?>
    {
        private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public ValueTask<string?> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Infer(query, cancellationToken));
        }

        private static string? Infer(Query query, CancellationToken cancellationToken)
        {
            var ownedRoot = Normalize(query.ProjectionRoot);
            if (ownedRoot is null)
            {
                return null;
            }

            string? common = null;
            foreach (var libraryPath in query.LibraryPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
}
