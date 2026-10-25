namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using Downloads;
using global::TransmissionStreaming.TestSupport.Arr;
using global::TransmissionStreaming.TestSupport.Torrents;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>Composes the disposable application and owns every resource through the xUnit fixture lifecycle.</summary>
public sealed class JellyfinTestServer(JellyfinApplication application) : IAsyncLifetime
{
    private readonly DownloadServices containers = application.Downloads;
    private readonly JellyfinHost host = application.Host;
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
            server = await host.GetOrStartAsync(containers, application.Baseline, deadline.Token);
            api = new JellyfinApi(Address);
            await api.AuthenticateAsync();
            // A fresh viewer owns this scenario's history; the infrastructure administrator survives resets.
            var viewer = await api.RequestAsync("/Users/New", new
            {
                Name = "scenario-" + Guid.NewGuid().ToString("N")
            });
            administrator = JellyfinApi.Value<string>(viewer, "Id");
            var policy = JellyfinApi.Field(viewer, "Policy")!.DeepClone().AsObject();
            policy["IsAdministrator"] = true;
            await api.RequestAsync($"/Users/{administrator}/Policy", policy);
            var media = host.ReferenceVideo;
            await File.WriteAllBytesAsync(Path.Combine(workspace, "reference.mkv"), media, deadline.Token);
            await Swarm.AddAsync(media, "Prototype Movie.mkv", deadline.Token);
            Arr = await ArrStub.StartAsync(workspace, containers.Network, Swarm, deadline.Token);
            ComposeAdapters();
            await downloads.ConfigureAsync();
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
            await Swarm.AllowFirstPieceAsync(TestContext.Current.CancellationToken);
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
        await api.AuthenticateAsync();
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
            if (api is not null && server is not null)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                await host.ResetAsync(api, deadline.Token);
            }
        }
        finally
        {
            api?.Dispose();
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
