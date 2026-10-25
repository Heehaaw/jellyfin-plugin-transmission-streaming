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
        return hevc.SelectMany(codec => codec.Conditions ?? []).Any(condition =>
            condition.Property == ProfileConditionValue.VideoRangeType
            && condition.Condition is ProfileConditionType.Equals or ProfileConditionType.EqualsAny
            && (condition.Value ?? "").Split(['|', ','], StringSplitOptions.TrimEntries).Any(value => value.Equals("HDR10", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasHevc(string? codecs)
    {
        var values = codecs?.Split(',', StringSplitOptions.TrimEntries);
        return values?.Any(codec => codec.Equals("hevc", StringComparison.OrdinalIgnoreCase) || codec.Equals("h265", StringComparison.OrdinalIgnoreCase)) == true;
    }
}
