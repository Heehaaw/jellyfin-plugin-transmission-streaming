namespace TransmissionStreaming.Logic.Discovery;

using global::TransmissionStreaming.Core.Transmission;

/// <summary>Selects video payloads without turning a sample or an extra into the primary movie.</summary>
internal sealed class VideoFileSelection : IVideoFileSelection
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".avi", ".ts", ".webm", ".mov"
    };

    /// <summary>Accepts supported video files and excludes conventional sample and trailer names.</summary>
    public bool IsPlayable(string name)
    {
        var segments = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !VideoExtensions.Contains(Path.GetExtension(segments[^1])))
        {
            return false;
        }

        if (segments[..^1].Any(IsSupplementFolder))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(segments[^1]);
        var tokens = stem.Split(['.', ' ', '_', '-', '[', ']', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        return !tokens.Any(token => token.Equals("sample", StringComparison.OrdinalIgnoreCase)
                                    || token.Equals("trailer", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns the largest eligible video in torrent order, or null when none is playable.</summary>
    public int? PrimaryMovieIndex(IReadOnlyList<TorrentFileSnapshot> files)
    {
        int? selected = null;
        long largest = 0;
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            if (file.Length <= largest || !IsPlayable(file.Name))
            {
                continue;
            }

            selected = index;
            largest = file.Length;
        }
        return selected;
    }

    private static bool IsSupplementFolder(string name)
    {
        return name.Equals("sample", StringComparison.OrdinalIgnoreCase)
        || name.Equals("samples", StringComparison.OrdinalIgnoreCase)
        || name.Equals("trailer", StringComparison.OrdinalIgnoreCase)
        || name.Equals("trailers", StringComparison.OrdinalIgnoreCase);
    }
}
