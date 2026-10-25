namespace TransmissionStreaming.Packaging;

internal sealed record BuiltPlugin(string Directory, PluginDescriptor Descriptor);

internal static class PluginBuild
{
    public static async Task<PluginDescriptor> GenerateMetadataAsync(RepositoryLayout layout, Version version, string outputPath, CancellationToken cancellationToken)
    {
        // This target evaluates release metadata only; it does not rebuild the verified assemblies.
        await DotnetCommand.RunAsync(
            workingDirectory: layout.Root,
            arguments:
            [
                "msbuild", layout.ProjectPath, "-target:GeneratePluginMetadata", $"-property:Version={version}",
                $"-property:PluginMetadataOutputPath={outputPath}", "-nologo", "-verbosity:quiet"
            ],
            quiet: true,
            cancellationToken: cancellationToken
        );
        return await PluginDescriptor.ReadAsync(outputPath, cancellationToken);
    }

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

        var metadataPath = Path.Combine(outputDirectory, PluginDescriptor.FileName);
        var descriptor = await PluginDescriptor.ReadAsync(metadataPath, cancellationToken);
        return new BuiltPlugin(outputDirectory, descriptor);
    }
}
