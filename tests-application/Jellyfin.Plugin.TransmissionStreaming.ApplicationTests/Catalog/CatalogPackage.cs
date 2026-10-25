namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using Hosting;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Creates the same flat ZIP and manifest shape consumed by Jellyfin's package manager.</summary>
internal static class CatalogPackage
{
    public static void Write(string destination, Uri packageUrl, bool corruptChecksum)
    {
        var bundle = PluginBundle.BuildOutput;
        Directory.CreateDirectory(destination);
        var packagePath = Path.Combine(destination, "package.zip");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            foreach (var name in bundle.Assemblies.Append("meta.json"))
            {
                archive.CreateEntryFromFile(Path.Combine(bundle.Directory, name), name);
            }
        }

        using var zip = File.OpenRead(packagePath);
        // Jellyfin's repository protocol requires MD5, rather than a security signature.
        var checksum = Convert.ToHexStringLower(MD5.HashData(zip));
        if (corruptChecksum)
        {
            checksum = (checksum[0] == '0' ? "1" : "0") + checksum[1..];
        }

        var metadata = JsonNode.Parse(File.ReadAllText(Path.Combine(bundle.Directory, "meta.json")));
        var manifest = new[]
        {
            new
            {
                guid = bundle.Id,
                name = bundle.Name,
                description = JellyfinApi.Value<string>(metadata, "description"),
                owner = JellyfinApi.Value<string>(metadata, "owner"),
                category = JellyfinApi.Value<string>(metadata, "category"),
                versions = new[]
                {
                    new
                    {
                        version = bundle.Version,
                        targetAbi = JellyfinApi.Value<string>(metadata, "targetAbi"),
                        sourceUrl = packageUrl.AbsoluteUri,
                        checksum,
                        timestamp = DateTimeOffset.UtcNow,
                        changelog = "Disposable application test package."
                    }
                }
            }
        };
        File.WriteAllText(Path.Combine(destination, "manifest.json"), JsonSerializer.Serialize(manifest));
    }
}
