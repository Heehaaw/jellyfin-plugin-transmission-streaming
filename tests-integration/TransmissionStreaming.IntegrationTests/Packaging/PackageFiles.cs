namespace TransmissionStreaming.IntegrationTests.Packaging;

using global::TransmissionStreaming.Packaging;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Owns synthetic package files without invoking a build or an external process.</summary>
internal sealed class PackageFiles : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public PackageFiles()
    {
        Directory.CreateDirectory(directory);
    }

    public string OutputDirectory => Path.Combine(directory, "output");

    public string CreateArchive(PluginDescriptor descriptor, byte[] payload, string? omittedAssembly = null)
    {
        var archivePath = Path.Combine(directory, "source.zip");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var assembly in descriptor.Assemblies)
        {
            if (assembly == omittedAssembly)
            {
                continue;
            }

            var entry = archive.CreateEntry(assembly);
            using var stream = entry.Open();
            stream.Write(payload);
            // Distinct synthetic member bytes make accidental DLL substitution observable.
            stream.Write(Encoding.UTF8.GetBytes(assembly));
        }

        var metadata = archive.CreateEntry(PluginDescriptor.FileName);
        using var metadataStream = metadata.Open();
        JsonSerializer.Serialize(metadataStream, descriptor, JsonOptions);
        return archivePath;
    }

    public string WriteMetadata(PluginDescriptor descriptor)
    {
        var metadataDirectory = Path.Combine(directory, "metadata");
        Directory.CreateDirectory(metadataDirectory);
        var metadataPath = Path.Combine(metadataDirectory, PluginDescriptor.FileName);
        var contents = JsonSerializer.Serialize(descriptor, JsonOptions);
        File.WriteAllText(metadataPath, contents);
        return metadataPath;
    }

    public Dictionary<string, string> ReadMemberHashes(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            var hash = SHA256.HashData(stream);
            hashes.Add(entry.FullName, Convert.ToHexString(hash));
        }

        return hashes;
    }

    public void Dispose()
    {
        // Generated arguments can be disposed again by xUnit after the local ownership scope.
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
