namespace TransmissionStreaming.Packaging;

internal sealed record InstallOptions(string PluginsDirectory, string Configuration)
{
    public static InstallOptions Parse(string[] arguments)
    {
        string? pluginsDirectory = null;
        var configuration = "Debug";
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
                default:
                    throw new ArgumentException("Usage: Install.cs --plugins-dir PLUGINS_DIRECTORY [--configuration Debug|Release]");
            }
        }

        if (pluginsDirectory is null || string.IsNullOrWhiteSpace(configuration))
        {
            throw new ArgumentException("An explicit --plugins-dir and a nonempty build configuration are required.");
        }

        return new InstallOptions
        (
            PluginsDirectory: pluginsDirectory,
            Configuration: configuration
        );
    }
}
