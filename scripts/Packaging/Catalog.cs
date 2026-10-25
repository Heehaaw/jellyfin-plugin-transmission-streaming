namespace TransmissionStreaming.Packaging;

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed record CatalogPackage(Guid Guid, string Name, string Description, string Owner, string Category, string Overview, CatalogVersion[] Versions);
internal sealed record CatalogVersion(string Version, string TargetAbi, Uri SourceUrl, string Checksum);

internal static class Catalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<string> WriteAsync(PluginDescriptor descriptor, string archivePath, PackageOptions options, CancellationToken cancellationToken)
    {
        var previous = options.PreviousManifestUrl is null
            ? []
            : await ReadPreviousAsync(options.PreviousManifestUrl, cancellationToken);
        var existing = previous.SingleOrDefault(package => package.Guid == descriptor.Guid);
        if (existing?.Versions.Any(version => version.Version == descriptor.Version) == true)
        {
            throw new InvalidOperationException("The catalog already contains this plugin version. Choose a new release tag before publishing another release.");
        }

        await using var archive = File.OpenRead(archivePath);
        // MD5 is the checksum required by Jellyfin's repository protocol, not a signature or a trust decision.
        var checksumBytes = await MD5.HashDataAsync(archive, cancellationToken);
        var checksum = Convert.ToHexStringLower(checksumBytes);
        var release = new CatalogVersion(descriptor.Version, descriptor.TargetAbi, options.SourceUrl!, checksum);
        var package = CreatePackage(descriptor, existing, release);
        var manifest = previous.Where(value => value.Guid != descriptor.Guid).Append(package).ToArray();
        var archiveDirectory = Path.GetDirectoryName(archivePath)!;
        var manifestPath = Path.Combine(archiveDirectory, "manifest.json");
        var contents = JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine;
        await File.WriteAllTextAsync(manifestPath, contents, cancellationToken);
        return manifestPath;
    }

    private static async Task<CatalogPackage[]> ReadPreviousAsync(Uri address, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        using var response = await client.GetAsync(address, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<CatalogPackage[]>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The previous repository manifest is empty.");
    }

    private static CatalogPackage CreatePackage(PluginDescriptor descriptor, CatalogPackage? existing, CatalogVersion release)
    {
        var versions = (existing?.Versions ?? []).Append(release).OrderByDescending(version => Version.Parse(version.Version)).ToArray();
        return new CatalogPackage
        (
            Guid: descriptor.Guid,
            Name: descriptor.Name,
            Description: descriptor.Description,
            Owner: descriptor.Owner,
            Category: descriptor.Category,
            Overview: descriptor.Description,
            Versions: versions
        );
    }
}
