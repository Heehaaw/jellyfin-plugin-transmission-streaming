namespace TransmissionStreaming.Packaging;

using System.Runtime.CompilerServices;

internal sealed record RepositoryLayout(string Root)
{
    public string Plugin { get; init; } = "transmission-streaming";

    public string Scope => Plugin switch
    {
        "transmission-streaming" => "TransmissionStreaming",
        "hdr-playback" => "HdrPlayback",
        _ => throw new ArgumentException("Plugin must be transmission-streaming or hdr-playback.")
    };

    public string ProjectPath => Path.Combine(Root, "src", Scope, $"Jellyfin.Plugin.{Scope}", $"Jellyfin.Plugin.{Scope}.csproj");

    public string ArtifactsDirectory => Path.Combine(Root, "artifacts");

    public static RepositoryLayout FromEntry([CallerFilePath] string entryPath = "")
    {
        // File-based apps build outside the checkout; the source path keeps invocation independent of the working directory.
        var scriptsDirectory = Path.GetDirectoryName(entryPath)!;
        var root = Directory.GetParent(scriptsDirectory)!.FullName;
        return new RepositoryLayout(root);
    }
}
