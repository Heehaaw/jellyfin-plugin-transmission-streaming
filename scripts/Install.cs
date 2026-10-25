#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Packaging/*.cs

using TransmissionStreaming.Packaging;

return await ScriptHost.RunAsync(async cancellationToken =>
{
    var options = InstallOptions.Parse(args);
    var layout = RepositoryLayout.FromEntry();
    var bundle = await PluginBuild.BuildAsync
    (
        layout: layout,
        configuration: options.Configuration,
        cancellationToken: cancellationToken
    );
    var destination = PluginBundle.Install
    (
        plugin: bundle,
        pluginsDirectory: options.PluginsDirectory
    );

    Console.WriteLine($"Installed development bundle: {destination}");
    Console.WriteLine("Start or restart Jellyfin to load the updated plugin.");
});
