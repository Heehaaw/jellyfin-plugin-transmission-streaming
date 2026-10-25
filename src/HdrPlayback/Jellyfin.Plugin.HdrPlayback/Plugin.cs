namespace Jellyfin.Plugin.HdrPlayback;

using Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using Shared.Infrastructure.Packaging;

/// <summary>Adds optional HDR conversion to native Jellyfin video transcodes.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer, IPluginReleaseMetadata releaseMetadata) : base(paths, serializer)
    {
        var version = releaseMetadata.ReadVersion(AssemblyFilePath);
        if (version is not null)
        {
            SetAttributes(AssemblyFilePath, DataFolderPath, version);
        }
    }

    public override Guid Id => new("0427c746-6050-41ce-9441-d51e8d790182");
    public override string Name => "HDR Playback";
    public override string Description => "Optional SDR-to-HDR conversion during native Jellyfin video transcoding.";
}
