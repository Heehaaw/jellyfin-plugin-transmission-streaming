namespace Jellyfin.Plugin.TransmissionStreaming;

using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;


/// <summary>Progressive Transmission playback hosted inside Jellyfin.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = new(PluginConstants.Id);
    public static Plugin? Instance { get; private set; }
    public string StateDirectory { get; }

    public Plugin(IApplicationPaths paths, IXmlSerializer xmlSerializer) : base(paths, xmlSerializer)
    {
        Instance = this;
        StateDirectory = Path.Combine(paths.DataPath, PluginConstants.DataDirectory);
    }

    public override string Name => PluginConstants.Name;
    public override string Description => "Play arr-managed Transmission downloads before they finish.";
    public override Guid Id => PluginId;

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new() { Name = PluginConstants.SettingsPage, EmbeddedResourcePath = GetType().Namespace + ".Configuration.settings.html" }
    ];
}
