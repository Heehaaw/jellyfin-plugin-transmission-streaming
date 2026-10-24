namespace TransmissionStreaming.TestSupport.Torrents;

using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net.Http.Json;

/// <summary>Owns a real daemon and a seed whose advertised pieces are explicitly controlled by the test.</summary>
internal sealed class TorrentSwarm : IAsyncDisposable
{
    private readonly IContainer _transmission;
    private readonly IContainer _seed;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly TransmissionRpcClient rpc;

    private TorrentSwarm(IContainer transmission, IContainer seed, string workspace, string hash, byte[] media, string fileName)
    {
        _transmission = transmission;
        _seed = seed;
        // A mapped host port becomes available only after the daemon starts.
        rpc = new TransmissionRpcClient(_http, () => RpcUrl);
        Workspace = workspace;
        Hash = hash;
        ExpectedBytes = media;
        FileName = fileName;
    }

    public int PieceSize => SwarmSettings.PieceSize;
    public string Workspace { get; }
    public string Hash { get; }
    public string FileName { get; }
    public string Title => Path.GetFileNameWithoutExtension(FileName);
    public byte[] ExpectedBytes { get; }
    public Uri RpcUrl => new UriBuilder("http", _transmission.Hostname, _transmission.GetMappedPublicPort(SwarmSettings.RpcPort), SwarmSettings.RpcPath).Uri;
    public Uri ContainerRpcUrl => SwarmSettings.ContainerRpcUrl;
    public Uri ControlUrl => new UriBuilder("http", _seed.Hostname, _seed.GetMappedPublicPort(SwarmSettings.HttpPort)).Uri;
    public string LocalDownloadPath => Path.Combine(Workspace, SwarmSettings.CompletedDirectory, FileName);
    public string LocalPartialPath => Path.Combine(Workspace, SwarmSettings.IncompleteDirectory, FileName + ".part");

    public static async Task<TorrentSwarm> StartAsync(string workspace, INetwork network, byte[] media, string fileName, CancellationToken cancellationToken)
    {
        var files = await TorrentWorkspace.PrepareAsync(workspace, media, fileName, cancellationToken).ConfigureAwait(false);
        var seed = SwarmContainers.Seed(files.SeedRoot, network);
        var transmission = SwarmContainers.Transmission(workspace, network);
        var swarm = new TorrentSwarm(transmission, seed, workspace, files.Metainfo.Hash, media, fileName);
        try
        {
            await seed.StartAsync(cancellationToken).ConfigureAwait(false);
            await transmission.StartAsync(cancellationToken).ConfigureAwait(false);
            await swarm.rpc.CallAsync("torrent_add", new
            {
                metainfo = Convert.ToBase64String(files.Metainfo.Bytes),
                download_dir = SwarmSettings.CompletedRoot,
                paused = false
            }, cancellationToken).ConfigureAwait(false);
            return swarm;
        }
        catch
        {
            try
            {
                Console.Error.WriteLine(await swarm.LogsAsync(CancellationToken.None).ConfigureAwait(false));
            }
            finally
            {
                await swarm.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    public async Task AllowFirstPieceAsync(CancellationToken cancellationToken)
    {
        await ControlAsync("first", new { }, cancellationToken).ConfigureAwait(false);
        await Poll.UntilAsync(() => ProgressAsync(cancellationToken), state => state.FirstPieceAvailable,
            "Transmission to verify the first seeded piece", cancellationToken).ConfigureAwait(false);
    }

    public async Task AllowAllPiecesAsync(CancellationToken cancellationToken)
    {
        await ControlAsync("all", new { }, cancellationToken).ConfigureAwait(false);
        await Poll.UntilAsync(() => ProgressAsync(cancellationToken), state => state.Complete && File.Exists(LocalDownloadPath),
            "Transmission to complete and move the synthetic download", cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await rpc.CallAsync("torrent_remove", new { ids = new[] { Hash }, delete_local_data = false }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TorrentProgress> ProgressAsync(CancellationToken cancellationToken)
    {
        var result = await rpc.CallAsync("torrent_get", new
        {
            ids = new[] { Hash },
            fields = new[] { "percent_done", "pieces", "download_dir", "sequential_download", "error_string" }
        }, cancellationToken).ConfigureAwait(false);
        var torrent = result.GetProperty("torrents").EnumerateArray().Single();
        return new TorrentProgress(torrent.GetProperty("percent_done").GetDouble(),
            Convert.FromBase64String(torrent.GetProperty("pieces").GetString()!),
            torrent.GetProperty("download_dir").GetString()!, torrent.GetProperty("sequential_download").GetBoolean(),
            torrent.GetProperty("error_string").GetString()!);
    }

    public async Task ControlAsync(string operation, object value, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(new Uri(ControlUrl, "control/" + operation), value, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        return ContainerDiagnostics.ReadAsync(_transmission, _seed, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        try
        {
            await _transmission.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _seed.DisposeAsync().ConfigureAwait(false);
        }
    }

}
