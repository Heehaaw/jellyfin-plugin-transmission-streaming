namespace TransmissionStreaming.TestSupport.Torrents;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net.Http.Json;
using Xunit;

/// <summary>Owns one real downloader and controlled peer per assembly, with an exclusive fresh-data lease for each scenario.</summary>
public sealed class DownloadServices : IAsyncLifetime
{
    private readonly SemaphoreSlim scenarios = new(1, 1);

    private readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private INetwork? network;
    private Task? startup;
    private IContainer? seed;
    private IContainer? transmission;
    private TransmissionRpcClient rpc = null!;

    internal string Workspace { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache",
        "transmission-streaming-tests",
        "assembly-" + Guid.NewGuid().ToString("N")
    );

    internal INetwork Network => network ?? throw new InvalidOperationException("Download services have not started.");
    internal Uri RpcUrl => new UriBuilder("http", transmission!.Hostname, transmission.GetMappedPublicPort(SwarmSettings.RpcPort), SwarmSettings.RpcPath).Uri;
    internal Uri ControlUrl => new UriBuilder("http", seed!.Hostname, seed.GetMappedPublicPort(SwarmSettings.HttpPort)).Uri;
    internal TransmissionRpcClient Rpc => rpc;

    /// <summary>Defers shared infrastructure until a scenario actually needs the downloader.</summary>
    public ValueTask InitializeAsync()
    {
        // Assembly fixtures also support package and host checks that never require Docker.
        return ValueTask.CompletedTask;
    }

    private async Task StartSharedAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var phase = "workspace preparation";
        try
        {
            var bootstrapMedia = new byte[SwarmSettings.PieceSize * 3];
            var bootstrap = await TorrentWorkspace.PrepareAsync(Workspace, bootstrapMedia, "bootstrap.mkv", deadline.Token);
            phase = "network creation";
            network = new NetworkBuilder().Build();
            await network.CreateAsync(deadline.Token);
            phase = "container construction";
            seed = SwarmContainers.Seed(bootstrap.SeedRoot, network);
            transmission = SwarmContainers.Transmission(Workspace, network);
            phase = "container startup and readiness";
            await Task.WhenAll(seed.StartAsync(deadline.Token), transmission.StartAsync(deadline.Token));
            rpc = new TransmissionRpcClient(http, () => RpcUrl);
        }
        catch (Exception exception)
        {
            await WriteStartupDiagnosticsAsync(phase, exception.GetType().Name, cancellationToken.IsCancellationRequested, deadline.IsCancellationRequested);
            throw;
        }
    }

    private async Task WriteStartupDiagnosticsAsync(string phase, string failureType, bool callerCanceled, bool startupCanceled)
    {
        try
        {
            Console.Error.WriteLine(
                $"Downloader startup failed: phase={phase}; failure={failureType}; callerCanceled={callerCanceled}; startupDeadlineCanceled={startupCanceled}."
            );
            // The startup token is already canceled on a timeout. Diagnostics have their own bounded lifetime.
            var logs = await ContainerDiagnostics.ReadAsync(transmission, seed, CancellationToken.None);
            Console.Error.WriteLine(logs);
        }
        catch
        {
            // Neither Docker diagnostics nor a failed console write may replace the original startup exception.
        }
    }

    internal async Task<TorrentScenario> BeginScenarioAsync(CancellationToken cancellationToken)
    {
        await scenarios.WaitAsync(cancellationToken);
        try
        {
            // The exclusive scenario lease serializes startup and retains a failure for subsequent callers.
            startup ??= StartSharedAsync(cancellationToken);
            await startup;
            await RemoveTorrentsAsync(cancellationToken);
            var path = Path.Combine(Workspace, "scenarios", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TorrentScenario(this, path);
        }
        catch
        {
            scenarios.Release();
            throw;
        }
    }

    internal async Task<TorrentScenario> OpenAsync(byte[] media, string fileName, CancellationToken cancellationToken, bool completed = false)
    {
        var scenario = await BeginScenarioAsync(cancellationToken);
        try
        {
            await scenario.AddAsync(media, fileName, cancellationToken, completed);
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    internal string RemotePath(string localPath)
    {
        var relative = Path.GetRelativePath(Workspace, localPath).Replace(Path.DirectorySeparatorChar, '/');
        if (relative.StartsWith("../", StringComparison.Ordinal) || relative == "..")
        {
            throw new InvalidOperationException("Scenario paths must stay under the assembly media root.");
        }

        return SwarmSettings.RemoteRoot + "/" + relative;
    }

    internal async Task ControlAsync(string operation, object value, CancellationToken cancellationToken)
    {
        var address = new Uri(ControlUrl, "control/" + operation);
        using var response = await http.PostAsJsonAsync(address, value, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    internal Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        return ContainerDiagnostics.ReadAsync(transmission, seed, cancellationToken);
    }

    internal async ValueTask EndScenarioAsync(string workspace)
    {
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await RemoveTorrentsAsync(deadline.Token);
            }
            finally
            {
                try
                {
                    await ControlAsync("end", new { }, deadline.Token);
                }
                finally
                {
                    Directory.Delete(workspace, recursive: true);
                }
            }
        }
        finally
        {
            scenarios.Release();
        }
    }

    private async Task RemoveTorrentsAsync(CancellationToken cancellationToken)
    {
        var result = await rpc.CallAsync(
            method: "torrent_get",
            parameters: new
            {
                fields = new[]
                {
                    "hash_string"
                }
            },
            cancellationToken: cancellationToken
        );
        var torrents = result.GetProperty("torrents").EnumerateArray();
        var hashes = torrents.Select(torrent => torrent.GetProperty("hash_string").GetString()!).ToArray();
        if (hashes.Length > 0)
        {
            await rpc.CallAsync(
                method: "torrent_remove",
                parameters: new
                {
                    ids = hashes,
                    delete_local_data = false
                },
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>Releases both processes, their isolated network, and every remaining scenario directory.</summary>
    public async ValueTask DisposeAsync()
    {
        http.Dispose();
        try
        {
            await DisposeContainersAsync();
        }
        finally
        {
            try
            {
                if (network is not null)
                {
                    await network.DisposeAsync();
                }
            }
            finally
            {
                scenarios.Dispose();
                if (Directory.Exists(Workspace))
                {
                    Directory.Delete(path: Workspace, recursive: true);
                }
            }
        }
    }

    private async ValueTask DisposeContainersAsync()
    {
        try
        {
            if (transmission is not null)
            {
                await transmission.DisposeAsync();
            }
        }
        finally
        {
            if (seed is not null)
            {
                await seed.DisposeAsync();
            }
        }
    }
}
