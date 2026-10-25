namespace Jellyfin.Plugin.HdrPlayback.Packaging;

using System.Text.Json;

/// <summary>Reads the installed package version independently of its compiled assembly identity.</summary>
public interface IPluginReleaseMetadata
{
    /// <summary>Reads the version from meta.json beside the specified assembly.</summary>
    /// <returns>The package version, or null when the metadata file or its directory is absent.</returns>
    /// <exception cref="InvalidDataException">Metadata has no valid string version.</exception>
    /// <exception cref="JsonException">The metadata document is malformed.</exception>
    Version? ReadVersion(string assemblyFilePath);
}

internal sealed class PluginReleaseMetadata : IPluginReleaseMetadata
{
    private const string MetadataFileName = "meta.json";

    public Version? ReadVersion(string assemblyFilePath)
    {
        var assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyFilePath))!;
        var metadataPath = Path.Combine(assemblyDirectory, MetadataFileName);
        try
        {
            using var stream = File.OpenRead(metadataPath);
            using var document = JsonDocument.Parse(stream);
            return ParseVersion(document.RootElement);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static Version ParseVersion(JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object || !metadata.TryGetProperty("version", out var value) || value.ValueKind != JsonValueKind.String ||
            !Version.TryParse(value.GetString(), out var version))
        {
            throw new InvalidDataException("Plugin metadata must contain a valid string version.");
        }

        return version;
    }
}
