namespace HdrPlayback.Infrastructure.Transcoding;

using Core.Transcoding;

internal sealed class HdrCommandTransformer : IHdrCommandTransformer
{
    // Modern Mesa exports VA-API frames through DRM; the final conversion clears incompatible surface metadata.
    internal const string ConversionFilter = "hwmap=derive_device=drm,format=drm_prime," +
                                             "libplacebo=format=p010le:colorspace=bt2020nc:color_primaries=bt2020:color_trc=smpte2084:" +
                                             "range=tv:tonemapping=spline:inverse_tonemapping=1," +
                                             "hwmap=derive_device=vaapi,format=vaapi,scale_vaapi=format=p010";

    public string Transform(string arguments)
    {
        var tokens = FfmpegArguments.Parse(arguments);
        var encoder = Find(tokens, IsVideoEncoder);
        if (encoder < 0 || tokens[encoder + 1] != "hevc_vaapi")
        {
            throw new NotSupportedException("HDR conversion requires the configured HEVC VA-API encoder.");
        }

        AddConversionFilter(tokens);
        Set(tokens, IsVideoProfile, "-profile:v:0", "main10");
        Set(tokens, token => token == "-color_primaries", "-color_primaries", "bt2020");
        Set(tokens, token => token == "-color_trc", "-color_trc", "smpte2084");
        Set(tokens, token => token == "-colorspace", "-colorspace", "bt2020nc");
        Set(tokens, token => token == "-color_range", "-color_range", "tv");
        return FfmpegArguments.Format(tokens);
    }

    private static void AddConversionFilter(List<string> tokens)
    {
        var filter = Find(tokens, token => token is "-vf" or "-filter:v" or "-filter:v:0");
        var complex = Find(tokens, token => token == "-filter_complex");
        if (filter >= 0 && complex < 0)
        {
            tokens[filter + 1] += "," + ConversionFilter;
            return;
        }

        if (complex >= 0 && filter < 0)
        {
            AppendToMappedVideo(tokens, complex);
            return;
        }

        throw new NotSupportedException("This native video filter layout is not supported for HDR conversion.");
    }

    private static void AppendToMappedVideo(List<string> tokens, int complex)
    {
        var mappings = Enumerable.Range(0, tokens.Count - 1)
            .Where(index => tokens[index] == "-map" && tokens[index + 1].StartsWith('[') && tokens[index + 1].EndsWith(']'))
            .ToArray();
        var graph = tokens[complex + 1];
        var finalStage = graph[(graph.LastIndexOf(',') + 1)..];
        if (mappings.Length == 0 && finalStage.StartsWith("scale_vaapi=", StringComparison.Ordinal) && finalStage.IndexOfAny([';', '[', ']']) < 0)
        {
            // Jellyfin's Vulkan subtitle compositor leaves its final VA-API video output unlabelled.
            tokens[complex + 1] += "," + ConversionFilter;
            return;
        }

        if (mappings.Length != 1 || graph.Contains("[hdr_playback]", StringComparison.Ordinal))
        {
            throw new NotSupportedException("This subtitle filter layout is not supported for HDR conversion.");
        }

        var mapped = mappings[0] + 1;
        var output = tokens[mapped];
        // Append after native scaling and subtitle composition, so their geometry and timing remain Jellyfin-owned.
        tokens[complex + 1] += $";{output}{ConversionFilter}[hdr_playback]";
        tokens[mapped] = "[hdr_playback]";
    }

    private static bool IsVideoEncoder(string token)
    {
        return token is "-vcodec" or "-c:v" or "-c:v:0" or "-codec:v" or "-codec:v:0";
    }

    private static bool IsVideoProfile(string token)
    {
        return token is "-profile:v" or "-profile:v:0";
    }

    private static int Find(List<string> tokens, Func<string, bool> predicate)
    {
        return tokens.FindIndex(0, Math.Max(0, tokens.Count - 1), token => predicate(token));
    }

    private static void Set(List<string> tokens, Func<string, bool> predicate, string option, string value)
    {
        var index = Find(tokens, predicate);
        if (index >= 0)
        {
            tokens[index + 1] = value;
            return;
        }

        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("The native FFmpeg command is empty.");
        }

        tokens.InsertRange(tokens.Count - 1, [option, value]);
    }
}
