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

    private readonly INetwork network = new NetworkBuilder().Build();
    private IContainer? seed;
    private IContainer? transmission;
    private TransmissionRpcClient rpc = null!;

    internal string Workspace
    {
        get;
    } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "transmission-streaming-tests", "assembly-" + Guid.NewGuid().ToString("N"));

    internal INetwork Network => network;
    internal Uri RpcUrl => new UriBuilder("http", transmission!.Hostname, transmission.GetMappedPublicPort(SwarmSettings.RpcPort), SwarmSettings.RpcPath).Uri;
    internal Uri ControlUrl => new UriBuilder("http", seed!.Hostname, seed.GetMappedPublicPort(SwarmSettings.HttpPort)).Uri;
    internal TransmissionRpcClient Rpc => rpc;

    /// <summary>Starts shared infrastructure without adding a test torrent or library identity.</summary>
    public async ValueTask InitializeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var cancellationToken = deadline.Token;
        try
        {
            var bootstrapMedia = new byte[SwarmSettings.PieceSize * 3];
            var bootstrap = await TorrentWorkspace.PrepareAsync(Workspace, bootstrapMedia, "bootstrap.mkv", cancellationToken);
            await network.CreateAsync(cancellationToken);
            seed = SwarmContainers.Seed(bootstrap.SeedRoot, network);
            transmission = SwarmContainers.Transmission(Workspace, network);
            await Task.WhenAll(seed.StartAsync(cancellationToken), transmission.StartAsync(cancellationToken));
            rpc = new TransmissionRpcClient(http, () => RpcUrl);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    internal async Task<TorrentScenario> BeginScenarioAsync(CancellationToken cancellationToken)
    {
        await scenarios.WaitAsync(cancellationToken);
        try
        {
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

    internal async Task<TorrentScenario> OpenAsync(byte[] media, string fileName, CancellationToken cancellationToken)
    {
        var scenario = await BeginScenarioAsync(cancellationToken);
        try
        {
            await scenario.AddAsync(media, fileName, cancellationToken);
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
        return ContainerDiagnostics.ReadAsync(transmission!, seed!, cancellationToken);
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
                    await ControlAsync(
                        operation: "end",
                        value: new
                        {
                        },
                        cancellationToken: deadline.Token
                    );
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
                await network.DisposeAsync();
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
