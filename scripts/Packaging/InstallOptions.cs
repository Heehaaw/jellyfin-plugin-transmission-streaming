namespace TransmissionStreaming.Packaging;

internal sealed record InstallOptions(string PluginsDirectory, string Configuration)
{
    public string Plugin { get; init; } = "transmission-streaming";

    public static InstallOptions Parse(string[] arguments)
    {
        string? pluginsDirectory = null;
        var configuration = "Debug";
        var plugin = "transmission-streaming";
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length)
            {
                throw new ArgumentException("Each installation option needs a value.");
            }

            switch (arguments[index])
            {
                case "--plugins-dir":
                    pluginsDirectory = Path.GetFullPath(arguments[index + 1]);
                    break;
                case "--configuration":
                    configuration = arguments[index + 1];
                    break;
                case "--plugin":
                    plugin = arguments[index + 1];
                    _ = new RepositoryLayout("")
                    {
                        Plugin = plugin
                    }.Scope;
                    break;
                default:
                    throw new ArgumentException("Usage: Install.cs [--plugin transmission-streaming|hdr-playback] --plugins-dir PLUGINS_DIRECTORY [--configuration Debug|Release]");
            }
        }

        if (pluginsDirectory is null || string.IsNullOrWhiteSpace(configuration))
        {
            throw new ArgumentException("An explicit --plugins-dir and a nonempty build configuration are required.");
        }

        return new InstallOptions(pluginsDirectory, configuration)
        {
            Plugin = plugin
        };
    }
}
