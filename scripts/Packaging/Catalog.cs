namespace TransmissionStreaming.Packaging;

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed record CatalogPackage(Guid Guid, string Name, string Description, string Owner, string Category, string Overview, CatalogVersion[] Versions);
internal sealed record CatalogVersion(string Version, string TargetAbi, Uri SourceUrl, string Checksum);

internal static class Catalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<string> WriteAsync(PluginDescriptor descriptor, string archivePath, PackageOptions options, CancellationToken cancellationToken)
    {
        var previous = options.PreviousManifestUrl is null ? [] : await ReadPreviousAsync(options.PreviousManifestUrl, cancellationToken);
        var existing = previous.SingleOrDefault(package => package.Guid == descriptor.Guid);
        if (existing?.Versions.Any(version => version.Version == descriptor.Version) == true)
        {
            throw new InvalidOperationException("The catalog already contains this plugin version. Increment metadata before publishing another release.");
        }

        await using var archive = File.OpenRead(archivePath);
        // MD5 is the checksum required by Jellyfin's repository protocol, not a signature or a trust decision.
        var checksum = Convert.ToHexStringLower(await MD5.HashDataAsync(archive, cancellationToken));
        var release = new CatalogVersion(descriptor.Version, descriptor.TargetAbi, options.SourceUrl!, checksum);
        var versions = (existing?.Versions ?? []).Append(release).OrderByDescending(version => Version.Parse(version.Version)).ToArray();
        var package = new CatalogPackage(descriptor.Guid, descriptor.Name, descriptor.Description, descriptor.Owner, descriptor.Category, descriptor.Description, versions);
        var manifest = previous.Where(value => value.Guid != descriptor.Guid).Append(package).ToArray();
        var manifestPath = Path.Combine(Path.GetDirectoryName(archivePath)!, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine, cancellationToken);
        return manifestPath;
    }

    private static async Task<CatalogPackage[]> ReadPreviousAsync(Uri address, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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
}
