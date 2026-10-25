namespace TransmissionStreaming.Packaging;

internal sealed record PackagedPlugin(string ArchivePath, PluginDescriptor Descriptor);

internal static class PluginPackage
{
    public static async Task<PackagedPlugin> CreateAsync(PackageOptions options, RepositoryLayout layout, CancellationToken cancellationToken)
    {
        var releaseVersion = options.ReleaseVersion;
        if (releaseVersion is null)
        {
            return await ReadOrBuildAsync(options.ArchivePath, layout, layout.ArtifactsDirectory, cancellationToken);
        }

        var staging = Directory.CreateTempSubdirectory("transmission-streaming-package-");
        try
        {
            // Keep a fallback development ZIP out of the release output directory.
            var source = await ReadOrBuildAsync(options.ArchivePath, layout, staging.FullName, cancellationToken);
            var metadataPath = Path.Combine(staging.FullName, PluginDescriptor.FileName);
            var descriptor = await PluginBuild.GenerateMetadataAsync(layout, releaseVersion, metadataPath, cancellationToken);
            var archivePath = await PluginBundle.RepackAsync(source.ArchivePath, metadataPath, descriptor, layout.ArtifactsDirectory, cancellationToken);
            return new PackagedPlugin(archivePath, descriptor);
        }
        finally
        {
            staging.Delete(recursive: true);
        }
    }

    private static async Task<PackagedPlugin> ReadOrBuildAsync(string? archivePath, RepositoryLayout layout, string outputDirectory, CancellationToken cancellationToken)
    {
        if (archivePath is not null)
        {
            var descriptor = await PluginBundle.ReadDescriptorAsync(archivePath, cancellationToken);
            return new PackagedPlugin(archivePath, descriptor);
        }

        var bundle = await PluginBuild.BuildAsync(layout, "Release", cancellationToken);
        var createdArchive = PluginBundle.CreateArchive(bundle, outputDirectory);
        return new PackagedPlugin(createdArchive, bundle.Descriptor);
    }
}
