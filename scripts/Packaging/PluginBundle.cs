namespace TransmissionStreaming.Packaging;

using System.IO.Compression;

internal static class PluginBundle
{
    public static async Task<PluginDescriptor> ReadDescriptorAsync(string archivePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.GetEntry(PluginDescriptor.FileName) ?? throw new InvalidDataException("The plugin ZIP needs metadata at its root.");
        await using var metadata = entry.Open();
        return await PluginDescriptor.ReadAsync(metadata, cancellationToken);
    }

    public static string CreateArchive(BuiltPlugin plugin, string artifactsDirectory)
    {
        Directory.CreateDirectory(artifactsDirectory);
        var archivePath = Path.Combine(artifactsDirectory, plugin.Descriptor.ArchiveName);
        using var stream = File.Create(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var fileName in BundleFiles(plugin.Descriptor))
        {
            // Jellyfin extracts directly into the version directory; the ZIP must have no enclosing folder.
            archive.CreateEntryFromFile(Path.Combine(plugin.Directory, fileName), fileName, CompressionLevel.Optimal);
        }
        return archivePath;
    }

    public static string Install(BuiltPlugin plugin, string pluginsDirectory)
    {
        var destination = Path.Combine(pluginsDirectory, plugin.Descriptor.InstallDirectoryName);
        Directory.CreateDirectory(destination);
        foreach (var fileName in BundleFiles(plugin.Descriptor))
        {
            File.Copy(Path.Combine(plugin.Directory, fileName), Path.Combine(destination, fileName), overwrite: true);
        }
        return destination;
    }

    private static IEnumerable<string> BundleFiles(PluginDescriptor descriptor)
    {
        return descriptor.Assemblies.Append(PluginDescriptor.FileName);
    }
}
