namespace TransmissionStreaming.Logic.Discovery;

using Core.Transmission;
using Mediator;

/// <summary>Selects eligible torrent payloads while identifying the primary movie.</summary>
public static class SelectVideoFiles
{
    /// <summary>Requests playable file indexes and the largest eligible movie in torrent order.</summary>
    public sealed record Query(IReadOnlyList<TorrentFileSnapshot> Files) : IQuery<Result>;

    /// <summary>Episode discovery can use every playable index; movie discovery uses only the primary index.</summary>
    public sealed record Result(IReadOnlyList<int> PlayableIndexes, int? PrimaryMovieIndex);

    internal sealed class Handler : IQueryHandler<Query, Result>
    {
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv",
            ".mp4",
            ".m4v",
            ".avi",
            ".ts",
            ".webm",
            ".mov"
        };

        public ValueTask<Result> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var playable = new List<int>();
            int? primary = null;
            long largest = 0;
            for (var index = 0; index < query.Files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = query.Files[index];
                if (file.Length <= 0 || !IsPlayable(file.Name))
                {
                    continue;
                }

                playable.Add(index);
                // Equal-sized candidates retain the torrent's original ordering.
                if (file.Length > largest)
                {
                    largest = file.Length;
                    primary = index;
                }
            }

            return ValueTask.FromResult(new Result(playable, primary));
        }

        private static bool IsPlayable(string name)
        {
            var segments = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !VideoExtensions.Contains(Path.GetExtension(segments[^1])) || segments[..^1].Any(IsSupplementFolder))
            {
                return false;
            }

            var stem = Path.GetFileNameWithoutExtension(segments[^1]);
            var tokens = stem.Split(['.', ' ', '_', '-', '[', ']', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
            return !tokens.Any(token => token.Equals("sample", StringComparison.OrdinalIgnoreCase) || token.Equals("trailer", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSupplementFolder(string name)
        {
            return name.Equals("sample", StringComparison.OrdinalIgnoreCase) || name.Equals("samples", StringComparison.OrdinalIgnoreCase)
                                                                             || name.Equals("trailer", StringComparison.OrdinalIgnoreCase) ||
                                                                             name.Equals("trailers", StringComparison.OrdinalIgnoreCase);
        }
    }
}
