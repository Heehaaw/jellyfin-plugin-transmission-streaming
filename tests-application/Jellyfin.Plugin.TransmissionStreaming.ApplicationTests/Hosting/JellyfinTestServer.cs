namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using Downloads;
using global::TransmissionStreaming.TestSupport.Arr;
using global::TransmissionStreaming.TestSupport.Torrents;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>Composes the disposable application and owns every resource through the xUnit fixture lifecycle.</summary>
public sealed class JellyfinTestServer(DownloadServices containers, JellyfinBaseline baseline) : IAsyncLifetime
{
    private string workspace = null!;
    private JellyfinContainer? server;
    private JellyfinApi api = null!;
    private JellyfinLibrary library = null!;
    private StreamingScenario downloads = null!;
    private string administrator = "";

    internal TorrentScenario Swarm { get; private set; } = null!;

    internal ArrStub Arr { get; private set; } = null!;

    internal byte[] ExpectedBytes => Swarm.ExpectedBytes;
    internal string Workspace => workspace;
    internal string ProjectionMoviesPath => Swarm.RemotePath(Path.Combine(workspace, "projection", "movies"));
    internal string ImportedMoviesPath => Swarm.RemotePath(Path.Combine(workspace, "movies"));
    internal Uri Address => server!.Address;
    internal JellyfinApi Api => api;
    internal string Administrator => administrator;
    internal VideoDecoder Decoder => server!.Decoder;
    internal string ReferenceVideoPath => Swarm.RemotePath(Path.Combine(workspace, "reference.mkv"));

    internal string ProjectionFilePath
    {
        get
        {
            var root = Path.Combine(workspace, "projection");
            var localPath = Directory.EnumerateFiles(root, "*.strm", SearchOption.AllDirectories).Single();
            return Swarm.RemotePath(localPath);
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            Swarm = await containers.BeginScenarioAsync(deadline.Token);
            workspace = Swarm.Workspace;
            baseline.Populate(Path.Combine(workspace, "jellyfin"));
            server = new JellyfinContainer(workspace, containers.Network, true, containers.Workspace);
            await server.StartAsync(deadline.Token);
            api = new JellyfinApi(Address);
            administrator = await api.AuthenticateAsync();
            // This authenticated call proves that plugin discovery composed the real DI graph.
            await api.RequestAsync("/TransmissionStreaming/status");

            var media = await server.CreateVideoAsync(deadline.Token);
            await Swarm.AddAsync(media, "Prototype Movie.mkv", deadline.Token);
            Arr = await ArrStub.StartAsync(workspace, containers.Network, Swarm, deadline.Token);
            ComposeAdapters();
            await downloads.ConfigureAsync();
            await Swarm.AllowFirstPieceAsync(deadline.Token);
        }
        catch
        {
            await WriteDiagnosticsAsync();
            await DisposeAsync();
            throw;
        }
    }

    internal Task ConfigureAsync(bool enabled = true, int waitSeconds = 5)
    {
        return downloads.ConfigureAsync(enabled, waitSeconds);
    }

    internal async Task<string> WaitForStreamAsync()
    {
        try
        {
            return await downloads.WaitForStreamAsync();
        }
        catch (TimeoutException)
        {
            var status = await api.RequestAsync("/TransmissionStreaming/status");
            Console.Error.WriteLine("Discovery status: " + status);
            await WriteDiagnosticsAsync();
            throw;
        }
    }

    internal string HostStream(string permanentUrl)
    {
        return StreamingScenario.StreamForHost(permanentUrl, Address);
    }

    internal Task AddLibraryAsync(string name, params string[] paths)
    {
        return library.AddAsync(name, paths);
    }

    internal Task WaitForLibraryScanIdleAsync()
    {
        return new JellyfinLibraryScan(api).WaitForIdleAsync();
    }

    internal Task<JsonNode?> FindItemAsync(bool projected)
    {
        return library.FindAsync(projected);
    }

    internal Task<string> WaitForItemAsync(bool projected = true)
    {
        return library.WaitForItemAsync(projected);
    }

    internal Task<JsonNode> WaitForCompletionAsync(string projectionId)
    {
        return downloads.WaitForCompletionAsync(projectionId);
    }

    internal Task StageImportedFileAsync()
    {
        return downloads.StageImportedFileAsync();
    }

    internal Task ImportAsync()
    {
        return downloads.ImportAsync();
    }

    internal async Task RestartAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await server!.RestartAsync(deadline.Token);
        // Docker can assign another published port after restart. Server-to-server links
        // retain their network alias, while the test client must bind the current host port.
        api.Dispose();
        api = new JellyfinApi(Address);
        administrator = await api.AuthenticateAsync();
        ComposeAdapters();
        await api.RequestAsync("/TransmissionStreaming/status");
    }

    internal async Task<string> DiagnosticsAsync()
    {
        var hostLogs = server is null ? "" : await server.LogsAsync();
        var swarmLogs = Swarm is null ? "" : await Swarm.LogsAsync(CancellationToken.None);
        var lines = (hostLogs + swarmLogs).Split('\n');
        var tail = string.Join('\n', lines.TakeLast(100));
        // Jellyfin records stream URLs, so capability values must be removed from test output.
        return Regex.Replace(tail, "(?i)(key=)[^&\\s\\\"<>]+", "$1[redacted]");
    }

    internal static Task UntilAsync(Func<Task<bool>> probe, string description, int seconds = 45)
    {
        return TestWait.UntilAsync(probe, description, seconds);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            api?.Dispose();
            if (server is not null)
            {
                await server.DisposeAsync();
            }
        }
        finally
        {
            if (Swarm is not null)
            {
                await Swarm.DisposeAsync();
            }
        }
    }

    private void ComposeAdapters()
    {
        downloads = new StreamingScenario(workspace, api, Swarm, Arr);
        library = new JellyfinLibrary(api, administrator, Swarm.FileName);
    }

    private async Task WriteDiagnosticsAsync()
    {
        try
        {
            Console.Error.WriteLine(await DiagnosticsAsync());
        }
        catch
        {
            // Diagnostics must not replace the original startup or playback failure.
            Console.Error.WriteLine("Could not collect disposable application diagnostics.");
        }
    }
}
