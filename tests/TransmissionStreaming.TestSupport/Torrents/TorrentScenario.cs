namespace TransmissionStreaming.TestSupport.Torrents;

/// <summary>Owns fresh torrent files and seed state while borrowing assembly-owned processes.</summary>
internal sealed class TorrentScenario(DownloadServices services, string workspace) : IAsyncDisposable
{
    private int disposed;
    public string Workspace { get; } = workspace;
    public string MediaRoot => services.Workspace;
    public string RemoteRoot => services.RemotePath(Workspace);
    public string Hash { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string Title => Path.GetFileNameWithoutExtension(FileName);
    public byte[] ExpectedBytes { get; private set; } = [];
    public int PieceSize => SwarmSettings.PieceSize;
    public Uri RpcUrl => services.RpcUrl;
    public Uri ContainerRpcUrl => SwarmSettings.ContainerRpcUrl;
    public Uri ControlUrl => services.ControlUrl;
    public string LocalDownloadPath => Path.Combine(Workspace, SwarmSettings.CompletedDirectory, FileName);
    public string LocalPartialPath => Path.Combine(Workspace, SwarmSettings.IncompleteDirectory, FileName + ".part");

    public string RemotePath(string localPath)
    {
        return services.RemotePath(localPath);
    }

    public async Task AddAsync(byte[] media, string fileName, CancellationToken cancellationToken)
    {
        var files = await TorrentWorkspace.PrepareAsync(Workspace, media, fileName, cancellationToken, RemoteRoot);
        Hash = files.Metainfo.Hash;
        FileName = fileName;
        ExpectedBytes = media;
        await services.ControlAsync("scenario", new { directory = services.RemotePath(files.SeedRoot) }, cancellationToken);
        await services.Rpc.CallAsync("session_set", new
        {
            incomplete_dir = RemoteRoot + "/" + SwarmSettings.IncompleteDirectory
        }, cancellationToken);
        await services.Rpc.CallAsync("torrent_add", new
        {
            metainfo = Convert.ToBase64String(files.Metainfo.Bytes),
            download_dir = RemoteRoot + "/" + SwarmSettings.CompletedDirectory,
            paused = false
        }, cancellationToken);
    }

    public async Task AllowFirstPieceAsync(CancellationToken cancellationToken)
    {
        await ControlAsync("first", new { }, cancellationToken);
        await Poll.UntilAsync(() => ProgressAsync(cancellationToken), state => state.FirstPieceAvailable,
            "Transmission to verify the first seeded piece", cancellationToken);
    }

    public async Task AllowAllPiecesAsync(CancellationToken cancellationToken)
    {
        await ControlAsync("all", new { }, cancellationToken);
        await Poll.UntilAsync(() => ProgressAsync(cancellationToken), state => state.Complete && File.Exists(LocalDownloadPath),
            "Transmission to complete and move the synthetic download", cancellationToken);
    }

    public async Task<TorrentProgress> ProgressAsync(CancellationToken cancellationToken)
    {
        var result = await services.Rpc.CallAsync("torrent_get", new
        {
            ids = new[] { Hash },
            fields = new[] { "percent_done", "pieces", "download_dir", "sequential_download", "error_string" }
        }, cancellationToken);
        var torrent = result.GetProperty("torrents").EnumerateArray().Single();
        return new TorrentProgress(torrent.GetProperty("percent_done").GetDouble(), Convert.FromBase64String(torrent.GetProperty("pieces").GetString()!),
            torrent.GetProperty("download_dir").GetString()!, torrent.GetProperty("sequential_download").GetBoolean(), torrent.GetProperty("error_string").GetString()!);
    }

    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await services.Rpc.CallAsync("torrent_remove", new { ids = new[] { Hash }, delete_local_data = false }, cancellationToken);
    }

    public Task ControlAsync(string operation, object value, CancellationToken cancellationToken)
    {
        return services.ControlAsync(operation, value, cancellationToken);
    }

    public Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        return services.LogsAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return Interlocked.Exchange(ref disposed, 1) == 0 ? services.EndScenarioAsync(Workspace) : ValueTask.CompletedTask;
    }
}
