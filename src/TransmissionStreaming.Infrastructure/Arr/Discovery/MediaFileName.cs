namespace TransmissionStreaming.Infrastructure.Arr.Discovery;

using System.Globalization;
using System.Text.RegularExpressions;

// Keep episode parsing conservative: one torrent may contain many unrelated video files.
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

    private static readonly Regex SamplePattern = new(
        pattern: @"(?:^|[ ._-])sample(?:[ ._-]|$)",
        options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromMilliseconds(100)
    );

    public static bool IsPlayable(string filename)
    {
        var name = Basename(filename);
        return !SamplePattern.IsMatch(name) && new[]
        {
            ".mkv", ".mp4", ".m4v", ".avi", ".ts", ".webm"
        }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
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
