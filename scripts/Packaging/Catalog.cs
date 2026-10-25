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
            : await ReadPreviousAsync
            (
                address: options.PreviousManifestUrl,
                cancellationToken: cancellationToken
            );
        var existing = previous.SingleOrDefault(package => package.Guid == descriptor.Guid);
        if (existing?.Versions.Any(version => version.Version == descriptor.Version) == true)
        {
            throw new InvalidOperationException("The catalog already contains this plugin version. Increment metadata before publishing another release.");
        }

        await using var archive = File.OpenRead(archivePath);
        // MD5 is the checksum required by Jellyfin's repository protocol, not a signature or a trust decision.
        var checksumBytes = await MD5.HashDataAsync
        (
            source: archive,
            cancellationToken: cancellationToken
        );
        var checksum = Convert.ToHexStringLower(checksumBytes);
        var release = new CatalogVersion
        (
            Version: descriptor.Version,
            TargetAbi: descriptor.TargetAbi,
            SourceUrl: options.SourceUrl!,
            Checksum: checksum
        );
        var package = CreatePackage
        (
            descriptor: descriptor,
            existing: existing,
            release: release
        );
        var manifest = previous.Where(value => value.Guid != descriptor.Guid).Append(package).ToArray();
        var archiveDirectory = Path.GetDirectoryName(archivePath)!;
        var manifestPath = Path.Combine
        (
            path1: archiveDirectory,
            path2: "manifest.json"
        );
        var contents = JsonSerializer.Serialize
        (
            value: manifest,
            options: JsonOptions
        )
        + Environment.NewLine;
        await File.WriteAllTextAsync
        (
            path: manifestPath,
            contents: contents,
            cancellationToken: cancellationToken
        );
        return manifestPath;
    }

    private static async Task<CatalogPackage[]> ReadPreviousAsync(Uri address, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        using var response = await client.GetAsync
        (
            requestUri: address,
            cancellationToken: cancellationToken
        );
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<CatalogPackage[]>
               (
                   utf8Json: stream,
                   options: JsonOptions,
                   cancellationToken: cancellationToken
               )
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
