#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Packaging/*.cs

using TransmissionStreaming.Packaging;

return await ScriptHost.RunAsync(async cancellationToken =>
{
    var options = PackageOptions.Parse(args);
    var layout = RepositoryLayout.FromEntry();
    var bundle = await PluginBuild.BuildAsync(layout, "Release", cancellationToken);
    options.ValidateVersion(bundle.Descriptor.Version);

    var archivePath = PluginBundle.CreateArchive(bundle, layout.ArtifactsDirectory);
    Console.WriteLine($"Plugin ZIP: {archivePath}");
    if (options.SourceUrl is not null)
    {
        var manifestPath = await Catalog.WriteAsync(bundle.Descriptor, archivePath, options, cancellationToken);
        Console.WriteLine($"Repository manifest: {manifestPath}");
    }
});
