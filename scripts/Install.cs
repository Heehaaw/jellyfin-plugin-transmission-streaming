#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Packaging/*.cs

using TransmissionStreaming.Packaging;

return await ScriptHost.RunAsync(async cancellationToken =>
{
    var options = InstallOptions.Parse(args);
    var layout = RepositoryLayout.FromEntry() with
    {
        Plugin = options.Plugin
    };
    var bundle = await PluginBuild.BuildAsync(layout, options.Configuration, cancellationToken);
    var destination = PluginBundle.Install(bundle, options.PluginsDirectory);

    Console.WriteLine($"Installed development bundle: {destination}");
    Console.WriteLine("Start or restart Jellyfin to load the updated plugin.");
});
