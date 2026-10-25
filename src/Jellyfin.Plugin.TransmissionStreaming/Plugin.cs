namespace Jellyfin.Plugin.TransmissionStreaming;

using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

/// <summary>Progressive Transmission playback hosted inside Jellyfin.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IPluginSettings
{
    public static readonly Guid PluginId = new(PluginConstants.Id);

    public static Plugin? Instance
    {
        get;
        private set;
    }

    public string StateDirectory
    {
        get;
    }

    public Plugin(IApplicationPaths paths, IXmlSerializer xmlSerializer) : base(paths, xmlSerializer)
    {
        Instance = this;
        StateDirectory = Path.Combine(paths.DataPath, PluginConstants.DataDirectory);
    }

    public override string Name => PluginConstants.Name;
    public override string Description => "Play arr-managed Transmission downloads before they finish.";
    public override Guid Id => PluginId;

    event EventHandler<BasePluginConfiguration>? IPluginSettings.ConfigurationChanged
    {
        add => ConfigurationChanged += value;
        remove => ConfigurationChanged -= value;
    }

    void IPluginSettings.UpdateConfiguration(PluginConfiguration configuration)
    {
        UpdateConfiguration(configuration);
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var resourcePrefix = GetType().Namespace + ".Ui.";
        return
        [
            new()
            {
                Name = PluginConstants.SettingsPage,
                EmbeddedResourcePath = resourcePrefix + "settings.html"
            },
            new()
            {
                Name = PluginConstants.Ui.Styles,
                EmbeddedResourcePath = resourcePrefix + "settings.css"
            },
            new()
            {
                Name = PluginConstants.Ui.Controller,
                EmbeddedResourcePath = resourcePrefix + "controller.js"
            },
            new()
            {
                Name = PluginConstants.Ui.Configuration,
                EmbeddedResourcePath = resourcePrefix + "configuration.js"
            },
            new()
            {
                Name = PluginConstants.Ui.Mappings,
                EmbeddedResourcePath = resourcePrefix + "mappings.js"
            },
            new()
            {
                Name = PluginConstants.Ui.Activity,
                EmbeddedResourcePath = resourcePrefix + "activity.js"
            },
            new()
            {
                Name = PluginConstants.Ui.History,
                EmbeddedResourcePath = resourcePrefix + "history.js"
            },
            new()
            {
                Name = PluginConstants.Ui.Tooltips,
                EmbeddedResourcePath = resourcePrefix + "tooltips.js"
            },
            new()
            {
                Name = PluginConstants.Ui.TooltipDisplay,
                EmbeddedResourcePath = resourcePrefix + "tooltip-display.js"
            },
            new()
            {
                Name = PluginConstants.Ui.Elements,
                EmbeddedResourcePath = resourcePrefix + "elements.js"
            }
        ];
    }
}
