namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Serves a tag-stamped package and manifest through Jellyfin's repository protocol.</summary>
internal static class CatalogPackage
{
    public static async Task<string> WriteAsync(string destination, Uri packageUrl, bool corruptChecksum, Version version, CancellationToken cancellationToken)
    {
        var release = await TaggedCatalogPackage.CreateAsync(destination, version, cancellationToken);
        var metadata = release.Descriptor;
        var packagePath = Path.Combine(destination, "package.zip");
        File.Copy(release.ArchivePath, packagePath);

        await using var zip = File.OpenRead(packagePath);
        // Jellyfin's repository protocol requires MD5, rather than a security signature.
        var checksumBytes = await MD5.HashDataAsync(zip, cancellationToken);
        var checksum = Convert.ToHexStringLower(checksumBytes);
        if (corruptChecksum)
        {
            checksum = (checksum[0] == '0' ? "1" : "0") + checksum[1..];
        }

        var manifest = new[]
        {
            new
            {
                guid = metadata.Guid,
                name = metadata.Name,
                description = metadata.Description,
                owner = metadata.Owner,
                category = metadata.Category,
                versions = new[]
                {
                    new
                    {
                        version = metadata.Version,
                        targetAbi = metadata.TargetAbi,
                        sourceUrl = packageUrl.AbsoluteUri,
                        checksum,
                        timestamp = DateTimeOffset.UtcNow,
                        changelog = "Disposable application test package."
                    }
                }
            }
        };
        var manifestPath = Path.Combine(destination, "manifest.json");
        var contents = JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(manifestPath, contents, cancellationToken);
        return metadata.Version;
    }
}
