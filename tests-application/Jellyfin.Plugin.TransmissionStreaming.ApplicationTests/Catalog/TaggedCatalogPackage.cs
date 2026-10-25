namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using Hosting;
using global::TransmissionStreaming.Packaging;
using PackageBundle = global::TransmissionStreaming.Packaging.PluginBundle;

/// <summary>Promotes the actual master bundle through the production metadata-only release path.</summary>
internal static class TaggedCatalogPackage
{
    public static async Task<PackagedPlugin> CreateAsync(string directory, Version version, CancellationToken cancellationToken)
    {
        var bundle = Hosting.PluginBundle.BuildOutput;
        var metadataPath = Path.Combine(bundle.Directory, PluginDescriptor.FileName);
        var masterMetadata = await PluginDescriptor.ReadAsync(metadataPath, cancellationToken);
        var master = new BuiltPlugin(bundle.Directory, masterMetadata);
        var staging = Path.Combine(directory, "staging");
        var originalArchive = PackageBundle.CreateArchive(master, staging);

        var layout = new RepositoryLayout(DevelopmentInstall.FindRepositoryRoot());
        var releaseMetadataPath = Path.Combine(staging, PluginDescriptor.FileName);
        var releaseMetadata = await PluginBuild.GenerateMetadataAsync(layout, version, releaseMetadataPath, cancellationToken);
        var releaseDirectory = Path.Combine(staging, "release");
        var releasedArchive = await PackageBundle.RepackAsync(originalArchive, releaseMetadataPath, releaseMetadata, releaseDirectory, cancellationToken);
        return new PackagedPlugin(releasedArchive, releaseMetadata);
    }
}
