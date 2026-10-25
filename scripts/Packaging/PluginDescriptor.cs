namespace TransmissionStreaming.Packaging;

using System.Text.Json;

internal sealed record PluginDescriptor(Guid Guid, string Name, string Description, string Owner, string Category, string TargetAbi, string Version, string[] Assemblies)
{
    public const string FileName = "meta.json";
    public string InstallDirectoryName => $"{Name}_{Version}";

    public string ArchiveName
    {
        get
        {
            var nameCharacters = Name.Where(char.IsLetterOrDigit);
            var name = string.Concat(nameCharacters);
            return $"{name}_{Version}.zip";
        }
    }

    public static async Task<PluginDescriptor> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await ReadAsync
        (
            stream: stream,
            cancellationToken: cancellationToken
        );
    }

    public static async Task<PluginDescriptor> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var descriptor = await JsonSerializer.DeserializeAsync<PluginDescriptor>
                         (
                             utf8Json: stream,
                             options: options,
                             cancellationToken: cancellationToken
                         )
                         ?? throw new InvalidDataException("Plugin metadata is empty.");
        Validate(descriptor);
        return descriptor;
    }

    private static void Validate(PluginDescriptor descriptor)
    {
        var validVersion = System.Version.TryParse
        (
            input: descriptor.Version,
            result: out _
        );
        var validTargetAbi = System.Version.TryParse
        (
            input: descriptor.TargetAbi,
            result: out _
        );
        var invalidNameCharacters = Path.GetInvalidFileNameChars();
        if (descriptor.Guid == Guid.Empty || !validVersion || !validTargetAbi
            || string.IsNullOrWhiteSpace(descriptor.Name) || descriptor.Name.IndexOfAny(invalidNameCharacters) >= 0
            || descriptor.Assemblies is not { Length: > 0 })
        {
            throw new InvalidDataException("Plugin metadata needs a valid name, version, target ABI, and assembly list.");
        }

        foreach (var assembly in descriptor.Assemblies)
        {
            var hasDllExtension = assembly.EndsWith
            (
                value: ".dll",
                comparisonType: StringComparison.OrdinalIgnoreCase
            );
            if (Path.GetFileName(assembly) != assembly || !hasDllExtension)
            {
                throw new InvalidDataException("Plugin assembly entries must be plain DLL filenames.");
            }
        }
    }
}
