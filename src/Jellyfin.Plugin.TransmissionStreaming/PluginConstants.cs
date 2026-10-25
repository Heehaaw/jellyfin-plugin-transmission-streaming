namespace Jellyfin.Plugin.TransmissionStreaming;

/// <summary>Identifiers that must remain stable across builds and persisted configuration.</summary>
internal static class PluginConstants
{
    public const string Id = "843c341c-ce40-4cc3-8077-53ebd66d83b4";
    public const string Name = "Transmission Streaming";
    public const string SettingsPage = "transmission-streaming";
    public const string DataDirectory = "transmission-streaming";

    /// <summary>Native configuration-page resource names used by the browser module loader.</summary>
    internal static class Ui
    {
        public const string Styles = SettingsPage + ".css";
        public const string Controller = SettingsPage + "-controller.js";
        public const string Configuration = SettingsPage + "-configuration.js";
        public const string Mappings = SettingsPage + "-mappings.js";
        public const string Activity = SettingsPage + "-activity.js";
        public const string History = SettingsPage + "-history.js";
        public const string Tooltips = SettingsPage + "-tooltips.js";
        public const string TooltipDisplay = SettingsPage + "-tooltip-display.js";
        public const string Elements = SettingsPage + "-elements.js";
    }
}
