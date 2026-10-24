namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Downloads;
using global::TransmissionStreaming.TestSupport.Arr;
using global::TransmissionStreaming.TestSupport.Torrents;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>Composes the disposable application and owns every resource through the xUnit fixture lifecycle.</summary>
public sealed class JellyfinTestServer : IAsyncLifetime
{
    private readonly string workspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "transmission-streaming-tests", Guid.NewGuid().ToString("N"));
    private readonly INetwork network = new NetworkBuilder().Build();
    private JellyfinContainer? server;
    private JellyfinApi api = null!;
    private JellyfinLibrary library = null!;
    private StreamingScenario downloads = null!;
    private string administrator = "";

    internal TorrentSwarm Swarm { get; private set; } = null!;
    internal ArrStub Arr { get; private set; } = null!;
    internal byte[] ExpectedBytes => Swarm.ExpectedBytes;
    internal string Workspace => workspace;
    internal Uri Address => server!.Address;
    internal JellyfinApi Api => api;
    internal string Administrator => administrator;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(workspace);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(workspace, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            await network.CreateAsync(deadline.Token);
            server = new JellyfinContainer(workspace, network, true);
            await server.StartAsync(deadline.Token);
            api = new JellyfinApi(Address);
            administrator = await api.AuthenticateAsync();
            // This authenticated call proves that plugin discovery composed the real DI graph.
            await api.RequestAsync("/TransmissionStreaming/status");

            var media = await server.CreateVideoAsync(deadline.Token);
            Swarm = await TorrentSwarm.StartAsync(workspace, network, media, "Prototype Movie.mkv", deadline.Token);
            Arr = await ArrStub.StartAsync(workspace, network, Swarm, deadline.Token);
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
            Console.Error.WriteLine("Discovery status: " + await api.RequestAsync("/TransmissionStreaming/status"));
            await WriteDiagnosticsAsync();
            throw;
        }
    }

    internal string HostStream(string permanentUrl)
    {
        return StreamingScenario.StreamForHost(permanentUrl, Address);
    }

    internal Task AddLibraryAsync(string name, string path)
    {
        return library.AddAsync(name, path);
    }

    internal Task<JsonNode?> FindItemAsync(bool projected)
    {
        return library.FindAsync(projected);
    }

    internal Task<string> WaitForItemAsync(bool projected = true)
    {
        return library.WaitForItemAsync(projected);
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
        var tail = string.Join('\n', (hostLogs + swarmLogs).Split('\n').TakeLast(100));
        // Jellyfin records stream URLs, so capability values must be removed from test output.
        return Regex.Replace(tail, "(?i)(key=)[^&\\s\\\"<>]+", "$1[redacted]");
    }

    internal static Task UntilAsync(Func<Task<bool>> probe, string description, int seconds = 45)
    {
        return TestWait.UntilAsync(probe, description, seconds);
    }

    public async Task DisposeAsync()
    {
        // Each later owner is released even when an earlier shutdown throws.
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
            try
            {
                if (Swarm is not null)
                {
                    await Swarm.DisposeAsync();
                }
            }
            finally
            {
                try
                {
                    await network.DisposeAsync();
                }
                finally
                {
                    if (Directory.Exists(workspace))
                    {
                        Directory.Delete(workspace, true);
                    }
                }
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
