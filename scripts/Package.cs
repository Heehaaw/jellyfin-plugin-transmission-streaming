#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Packaging/*.cs

using TransmissionStreaming.Packaging;

return await ScriptHost.RunAsync(async cancellationToken =>
{
    var options = PackageOptions.Parse(args);
    var layout = RepositoryLayout.FromEntry();
    string archivePath;
    PluginDescriptor descriptor;
    if (options.ArchivePath is { } existingArchive)
    {
        // Release jobs can publish the verified CI bytes without rebuilding the plugin.
        archivePath = existingArchive;
        descriptor = await PluginBundle.ReadDescriptorAsync(archivePath, cancellationToken);
    }
    else
    {
        var bundle = await PluginBuild.BuildAsync(layout, "Release", cancellationToken);
        descriptor = bundle.Descriptor;
        archivePath = PluginBundle.CreateArchive(bundle, layout.ArtifactsDirectory);
    }

    options.ValidateVersion(descriptor.Version);
    Console.WriteLine($"Plugin ZIP: {archivePath}");
    if (options.SourceUrl is not null)
    {
        var manifestPath = await Catalog.WriteAsync(descriptor, archivePath, options, cancellationToken);
        Console.WriteLine($"Repository manifest: {manifestPath}");
    }
});
