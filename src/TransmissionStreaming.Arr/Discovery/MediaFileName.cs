namespace TransmissionStreaming.Arr.Discovery;

using System.Globalization;
using System.Text.RegularExpressions;


// Keep episode parsing conservative: one torrent may contain many unrelated video files.
internal static class MediaFileName
{
    private static readonly Regex EpisodePattern = new(
        @"(?<![a-z0-9])(?:s(?<season>\d{1,2})[ ._-]*e(?<episode>\d{1,3})|(?<season>\d{1,2})x(?<episode>\d{1,3}))(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static readonly Regex ExtraEpisodePattern = new(
        @"^(?:[ ._-]*e\d{1,3}(?!\d)|[- ]+\d{1,3}(?![a-z0-9])|x\d{1,3}(?!\d))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SamplePattern = new(
        @"(?:^|[ ._-])sample(?:[ ._-]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public static bool IsPlayable(string filename)
    {
        var name = Basename(filename);
        return !SamplePattern.IsMatch(name) && new[] { ".mkv", ".mp4", ".m4v", ".avi", ".ts", ".webm" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    }

    public static (int Season, int Episode)? ParseEpisode(string filename)
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

        return (int.Parse(match.Groups["season"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["episode"].Value, CultureInfo.InvariantCulture));
    }

    private static string Basename(string path) => Path.GetFileName(path.Replace('\\', '/'));
}
