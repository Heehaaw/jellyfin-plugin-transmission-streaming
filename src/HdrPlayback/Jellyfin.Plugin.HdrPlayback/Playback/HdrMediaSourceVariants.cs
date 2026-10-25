namespace Jellyfin.Plugin.HdrPlayback.Playback;

using MediaBrowser.Model.Dto;
using System.Text.Json;

/// <summary>Names transient conversion choices without adding library items or relabelling source video metadata.</summary>
internal static class HdrMediaSourceVariants
{
    internal const string Label = "HDR (converted)";
    internal const string Prefix = "hdr-conversion:";

    public static string? OriginalId(string? sourceId)
    {
        if (sourceId is null || !sourceId.StartsWith(Prefix, StringComparison.Ordinal) || sourceId.Length == Prefix.Length)
        {
            return null;
        }

        var original = sourceId[Prefix.Length..];
        return original.StartsWith(Prefix, StringComparison.Ordinal) ? null : original;
    }

    public static IReadOnlyList<MediaSourceInfo> AddChoices(IReadOnlyList<MediaSourceInfo> sources, IReadOnlySet<string> eligibleIds)
    {
        var choices = new List<MediaSourceInfo>(sources);
        foreach (var source in sources.Where(source => eligibleIds.Contains(source.Id) && OriginalId(source.Id) is null))
        {
            var id = Prefix + source.Id;
            if (string.IsNullOrEmpty(source.Id) || choices.Any(choice => choice.Id == id))
            {
                continue;
            }

            // A choice must renegotiate before playing; an original SDR URL must never masquerade as converted output.
            var converted = JsonSerializer.Deserialize<MediaSourceInfo>(JsonSerializer.Serialize(source))!;
            converted.Id = id;
            converted.Name = source.Name + " · " + Label;
            converted.SupportsDirectPlay = false;
            converted.SupportsDirectStream = false;
            converted.TranscodingUrl = null;
            choices.Add(converted);
        }

        return choices;
    }
}
