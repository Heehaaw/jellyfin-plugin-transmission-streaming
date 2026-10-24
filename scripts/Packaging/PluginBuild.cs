namespace TransmissionStreaming.Packaging;

internal sealed record BuiltPlugin(string Directory, PluginDescriptor Descriptor);

internal static class PluginBuild
{
    public static async Task<BuiltPlugin> BuildAsync(RepositoryLayout layout, string configuration, CancellationToken cancellationToken)
    {
        await DotnetCommand.RunAsync(layout.Root, ["build", layout.ProjectPath, "--configuration", configuration, "--nologo"], false, cancellationToken);
        // Ask MSBuild for the actual output path so framework and output-layout changes need no script edits.
        var outputDirectory = await DotnetCommand.RunAsync(layout.Root,
            ["msbuild", layout.ProjectPath, $"-property:Configuration={configuration}", "-getProperty:TargetDir", "-nologo"], true, cancellationToken);
        var descriptor = await PluginDescriptor.ReadAsync(Path.Combine(outputDirectory, PluginDescriptor.FileName), cancellationToken);
        return new BuiltPlugin(outputDirectory, descriptor);
    }
}
