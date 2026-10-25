namespace Jellyfin.Plugin.HdrPlayback.Playback;

using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;

internal sealed class HdrCapabilities(IServerConfigurationManager configuration, IMediaEncoder encoder)
{
    public bool PipelineSupported()
    {
        var options = (EncodingOptions)configuration.GetConfiguration("encoding");
        return options.HardwareAccelerationType == HardwareAccelerationType.vaapi
               && options.EnableHardwareEncoding && options.AllowHevcEncoding && encoder.IsVaapiDeviceAmd
               && encoder.IsVaapiDeviceSupportVulkanDrmInterop
               && encoder.SupportsEncoder("hevc_vaapi") && encoder.SupportsFilter("libplacebo") && encoder.SupportsHwaccel("vulkan");
    }

    public static bool ClientSupportsHdr(DeviceProfile? profile)
    {
        if (profile is null)
        {
            return false;
        }

        var hevc = (profile.CodecProfiles ?? []).Where(codec => codec.Type == CodecType.Video && HasHevc(codec.Codec));
        return hevc.Any(SupportsHdr10);
    }

    private static bool SupportsHdr10(CodecProfile codec)
    {
        var conditions = codec.Conditions ?? [];
        var ranges = conditions.Where(condition => condition.Property == ProfileConditionValue.VideoRangeType).ToArray();
        // Android declares hardware-supported Main10/HDR10 profiles, but its legacy profile omits VideoRangeType.
        var main10 = conditions.Any(condition =>
            condition.Property == ProfileConditionValue.VideoProfile
            && condition.Condition is ProfileConditionType.Equals or ProfileConditionType.EqualsAny
            && Values(condition).Any(value => value.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant() is "main10" or "main10hdr10" or "main10hdr10plus"));
        if (ranges.Length == 0)
        {
            return main10;
        }

        var permitted = ranges.All(condition => condition.Condition switch
        {
            ProfileConditionType.Equals or ProfileConditionType.EqualsAny => Values(condition).Contains("HDR10", StringComparer.OrdinalIgnoreCase),
            ProfileConditionType.NotEquals => !Values(condition).Contains("HDR10", StringComparer.OrdinalIgnoreCase),
            _ => false
        });
        return permitted && (main10 || ranges.Any(condition =>
            condition.Condition is ProfileConditionType.Equals or ProfileConditionType.EqualsAny
            && Values(condition).Contains("HDR10", StringComparer.OrdinalIgnoreCase)));
    }

    private static string[] Values(ProfileCondition condition)
    {
        return (condition.Value ?? "").Split(['|', ','], StringSplitOptions.TrimEntries);
    }

    private static bool HasHevc(string? codecs)
    {
        var values = codecs?.Split(',', StringSplitOptions.TrimEntries);
        return values?.Any(codec => codec.Equals("hevc", StringComparison.OrdinalIgnoreCase) || codec.Equals("h265", StringComparison.OrdinalIgnoreCase)) == true;
    }
}
