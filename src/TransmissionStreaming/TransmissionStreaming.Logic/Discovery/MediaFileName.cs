namespace TransmissionStreaming.Logic.Discovery;

using System.Globalization;
using System.Text.RegularExpressions;

// Reject ambiguous filenames before assigning episode ownership.
internal static class MediaFileName
{
    private static readonly Regex EpisodePattern = new(
        pattern: @"(?<![a-z0-9])(?:s(?<season>\d{1,2})[ ._-]*e(?<episode>\d{1,3})|(?<season>\d{1,2})x(?<episode>\d{1,3}))(?!\d)",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromMilliseconds(100)
    );

    private static readonly Regex ExtraEpisodePattern = new(
        pattern: @"^(?:[ ._-]*e\d{1,3}(?!\d)|[- ]+\d{1,3}(?![a-z0-9])|x\d{1,3}(?!\d))",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromMilliseconds(100)
    );

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

    public static bool IsPlayable(string name)
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
        return name.Equals("sample", StringComparison.OrdinalIgnoreCase) || name.Equals("samples", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("trailer", StringComparison.OrdinalIgnoreCase) || name.Equals("trailers", StringComparison.OrdinalIgnoreCase);
    }

    public static EpisodeNumber? ParseEpisode(string filename)
    {
        var name = Basename(filename);
        var matches = EpisodePattern.Matches(name);
        if (matches.Count != 1)
        {
            return null;
        }

        var match = matches[0];
        if (ExtraEpisodePattern.IsMatch(name[(match.Index + match.Length)..]))
        {
            return null;
        }

        var season = int.Parse(match.Groups["season"].Value, CultureInfo.InvariantCulture);
        var episode = int.Parse(match.Groups["episode"].Value, CultureInfo.InvariantCulture);
        return new EpisodeNumber(season, episode);
    }

    private static string Basename(string path)
    {
        return Path.GetFileName(path.Replace('\\', '/'));
    }
}

/// <summary>An unambiguous single episode parsed from a release filename.</summary>
internal readonly record struct EpisodeNumber(int Season, int Episode);
