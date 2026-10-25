#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Packaging/*.cs

using TransmissionStreaming.Packaging;

return await ScriptHost.RunAsync(async cancellationToken =>
{
    var options = PackageOptions.Parse(args);
    var layout = RepositoryLayout.FromEntry();
    var package = await PluginPackage.CreateAsync(options, layout, cancellationToken);
    options.ValidateVersion(package.Descriptor.Version);
    Console.WriteLine($"Plugin ZIP: {package.ArchivePath}");
    if (options.SourceUrl is not null)
    {
        var manifestPath = await Catalog.WriteAsync(package.Descriptor, package.ArchivePath, options, cancellationToken);
        Console.WriteLine($"Repository manifest: {manifestPath}");
    }
});
