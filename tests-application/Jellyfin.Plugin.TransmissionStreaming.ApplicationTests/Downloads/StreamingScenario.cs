namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Downloads;

using global::TransmissionStreaming.TestSupport.Arr;
using global::TransmissionStreaming.TestSupport.Torrents;
using Hosting;
using System.Text.Json.Nodes;

/// <summary>Controls download settings and the simulated arr import, without bypassing the plugin's HTTP endpoints.</summary>
internal sealed class StreamingScenario(string workspace, JellyfinApi api, TorrentScenario swarm, ArrStub arr)
{
    private const string RemoteRoot = "/data";
    private const string ProjectionDirectory = "projection";
    private const string ImportedDirectory = "movies";
    private string LocalProjection => Path.Combine(workspace, ProjectionDirectory);
    private string RemoteImportedFile => "/arr/library/" + swarm.FileName;

    public async Task ConfigureAsync(bool enabled = true, int waitSeconds = 5)
    {
        await api.RequestAsync(
            path: $"/Plugins/{PluginBundle.BuildOutput.Id}/Configuration",
            body: new
            {
                Enabled = enabled,
                TransmissionUrl = swarm.ContainerRpcUrl.ToString(),
                RadarrUrl = arr.ContainerUrl.ToString(),
                RadarrApiKey = arr.Key,
                SonarrUrl = "",
                SonarrApiKey = "",
                TransmissionUsername = "",
                TransmissionPassword = "",
                // Deliberately unrelated fallback roots prove that playback uses each service's explicit mapping.
                RemoteMediaRoot = "/unused-legacy",
                LocalMediaRoot = "/unused-legacy",
                PathMappings = new[]
                {
                    new
                    {
                        Service = 0,
                        RemoteRoot,
                        LocalRoot = RemoteRoot
                    },
                    new
                    {
                        Service = 1,
                        RemoteRoot = "/arr/library",
                        LocalRoot = swarm.RemotePath(Path.Combine(workspace, ImportedDirectory))
                    }
                },
                ProjectionRoot = swarm.RemotePath(LocalProjection),
                JellyfinPublicUrl = JellyfinContainer.InternalAddress.ToString(),
                PollSeconds = 2,
                PieceWaitSeconds = waitSeconds,
                PrioritizePlayback = true,
                ShowDownloadStatus = true
            }
        );
        var saved = await api.RequestAsync("/TransmissionStreaming/status");
        if (JellyfinApi.Value<bool>(saved, "Enabled") != enabled)
        {
            throw new InvalidOperationException("Jellyfin did not apply the plugin configuration.");
        }
    }

    public async Task<string> WaitForStreamAsync()
    {
        string? path = null;
        await TestWait.UntilAsync(
            probe: () =>
            {
                path = Directory.Exists(LocalProjection)
                    ? Directory.EnumerateFiles(LocalProjection, "*.strm", SearchOption.AllDirectories).FirstOrDefault()
                    : null;
                return Task.FromResult(path is not null);
            },
            description: "automatic download discovery"
        );
        return (await File.ReadAllTextAsync(path!)).Trim();
    }

    public static string StreamForHost(string permanentUrl, Uri hostAddress)
    {
        var uri = new Uri(permanentUrl);
        if (uri.Host != JellyfinContainer.NetworkAlias || uri.Port != JellyfinContainer.HttpPort)
        {
            throw new InvalidOperationException("Unexpected stream origin in disposable test.");
        }

        return new Uri(hostAddress, uri.PathAndQuery).ToString();
    }

    public async Task<JsonNode> WaitForCompletionAsync(string projectionId)
    {
        JsonNode? completed = null;
        await TestWait.UntilAsync(
            probe: async () =>
            {
                var status = await api.RequestAsync("/TransmissionStreaming/status");
                completed = JellyfinApi.Field(status, "Completed")!.AsArray()
                    .FirstOrDefault(entry => JellyfinApi.Value<string>(entry, "Id") == projectionId);
                return completed is not null;
            },
            description: "native library handoff and completion archive"
        );
        return completed!;
    }

    public async Task StageImportedFileAsync()
    {
        await swarm.AllowAllPiecesAsync(CancellationToken.None);
        var importedRoot = Directory.CreateDirectory(Path.Combine(workspace, ImportedDirectory)).FullName;
        File.Move(swarm.LocalDownloadPath, Path.Combine(importedRoot, swarm.FileName));
    }

    public async Task ImportAsync()
    {
        if (File.Exists(swarm.LocalDownloadPath))
        {
            await StageImportedFileAsync();
        }

        await arr.PublishImportAsync(RemoteImportedFile, CancellationToken.None);
        await TestWait.UntilAsync(
            probe: async () =>
            {
                var status = await api.RequestAsync("/TransmissionStreaming/status");
                return JellyfinApi.Field(status, "Entries")!.AsArray().Any(entry => JellyfinApi.Value<string>(entry, "Status") == "Complete");
            },
            description: "exact arr import while seeding"
        );
    }
}
