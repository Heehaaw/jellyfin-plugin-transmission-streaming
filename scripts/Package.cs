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
        descriptor = await PluginBundle.ReadDescriptorAsync
        (
            archivePath: archivePath,
            cancellationToken: cancellationToken
        );
    }
    else
    {
        var bundle = await PluginBuild.BuildAsync
        (
            layout: layout,
            configuration: "Release",
            cancellationToken: cancellationToken
        );
        descriptor = bundle.Descriptor;
        archivePath = PluginBundle.CreateArchive
        (
            plugin: bundle,
            artifactsDirectory: layout.ArtifactsDirectory
        );
    }

    options.ValidateVersion(descriptor.Version);
    Console.WriteLine($"Plugin ZIP: {archivePath}");
    if (options.SourceUrl is not null)
    {
        var manifestPath = await Catalog.WriteAsync
        (
            descriptor: descriptor,
            archivePath: archivePath,
            options: options,
            cancellationToken: cancellationToken
        );
        Console.WriteLine($"Repository manifest: {manifestPath}");
    }
});
