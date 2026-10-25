namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using System.Text.Json.Nodes;

/// <summary>Reads the built descriptor so tests use the same identity and assembly list as installation.</summary>
internal sealed record PluginBundle(string Id, string Name, string Version, string Directory, IReadOnlyList<string> Assemblies)
{
    public static PluginBundle BuildOutput { get; } = Read(Path.Combine(AppContext.BaseDirectory, "Plugin"));

    public string FolderName => $"{Name}_{Version}";

    public void InstallTo(string pluginsDirectory)
    {
        var destination = Path.Combine(pluginsDirectory, FolderName);
        System.IO.Directory.CreateDirectory(destination);
        foreach (var name in Assemblies.Append("meta.json"))
        {
            File.Copy(Path.Combine(Directory, name), Path.Combine(destination, name));
        }
    }

    private static PluginBundle Read(string directory)
    {
        var metadata = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "meta.json")));
        return new PluginBundle(
            Id: JellyfinApi.Value<string>(metadata, "guid"),
            Name: JellyfinApi.Value<string>(metadata, "name"),
            Version: JellyfinApi.Value<string>(metadata, "version"),
            Directory: directory,
            Assemblies: JellyfinApi.Field(metadata, "assemblies")!.AsArray().Select(value => value!.GetValue<string>()).ToArray()
        );
    }
}
