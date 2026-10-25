namespace TransmissionStreaming.Packaging;

internal sealed record BuiltPlugin(string Directory, PluginDescriptor Descriptor);

internal static class PluginBuild
{
    public static async Task<BuiltPlugin> BuildAsync(RepositoryLayout layout, string configuration, CancellationToken cancellationToken)
    {
        await DotnetCommand.RunAsync
        (
            workingDirectory: layout.Root,
            arguments: ["build", layout.ProjectPath, "--configuration", configuration, "--nologo"],
            quiet: false,
            cancellationToken: cancellationToken
        );

        // Ask MSBuild for the actual output path so framework and output-layout changes need no script edits.
        var outputDirectory = await DotnetCommand.RunAsync
        (
            workingDirectory: layout.Root,
            arguments: ["msbuild", layout.ProjectPath, $"-property:Configuration={configuration}", "-getProperty:TargetDir", "-nologo"],
            quiet: true,
            cancellationToken: cancellationToken
        );

        var metadataPath = Path.Combine
        (
            path1: outputDirectory,
            path2: PluginDescriptor.FileName
        );
        var descriptor = await PluginDescriptor.ReadAsync
        (
            path: metadataPath,
            cancellationToken: cancellationToken
        );
        return new BuiltPlugin
        (
            Directory: outputDirectory,
            Descriptor: descriptor
        );
    }
}
