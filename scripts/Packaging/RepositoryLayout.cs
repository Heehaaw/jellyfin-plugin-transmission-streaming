namespace TransmissionStreaming.Packaging;

using System.Runtime.CompilerServices;

internal sealed record RepositoryLayout(string Root)
{
    public string ProjectPath => Path.Combine(Root, "src", "TransmissionStreaming", "Jellyfin.Plugin.TransmissionStreaming", "Jellyfin.Plugin.TransmissionStreaming.csproj");

    public string ArtifactsDirectory => Path.Combine(Root, "artifacts");

    public static RepositoryLayout FromEntry([CallerFilePath] string entryPath = "")
    {
        // File-based apps build outside the checkout; the source path keeps invocation independent of the working directory.
        var scriptsDirectory = Path.GetDirectoryName(entryPath)!;
        var root = Directory.GetParent(scriptsDirectory)!.FullName;
        return new RepositoryLayout(root);
    }
}
