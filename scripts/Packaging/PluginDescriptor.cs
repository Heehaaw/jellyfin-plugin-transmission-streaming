namespace TransmissionStreaming.Packaging;

using System.Text.Json;

internal sealed record PluginDescriptor(Guid Guid, string Name, string Description, string Owner, string Category, string TargetAbi, string Version, string[] Assemblies)
{
    public const string FileName = "meta.json";
    public string InstallDirectoryName => $"{Name}_{Version}";
    public string ArchiveName => $"{string.Concat(Name.Where(char.IsLetterOrDigit))}_{Version}.zip";

    public static async Task<PluginDescriptor> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var descriptor = await JsonSerializer.DeserializeAsync<PluginDescriptor>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new InvalidDataException("Plugin metadata is empty.");
        if (descriptor.Guid == Guid.Empty || !System.Version.TryParse(descriptor.Version, out _) || !System.Version.TryParse(descriptor.TargetAbi, out _)
            || string.IsNullOrWhiteSpace(descriptor.Name) || descriptor.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || descriptor.Assemblies is not { Length: > 0 })
        {
            throw new InvalidDataException("Plugin metadata needs a valid name, version, target ABI, and assembly list.");
        }

        foreach (var assembly in descriptor.Assemblies)
        {
            if (Path.GetFileName(assembly) != assembly || !assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Plugin assembly entries must be plain DLL filenames.");
            }
        }
        return descriptor;
    }
}
