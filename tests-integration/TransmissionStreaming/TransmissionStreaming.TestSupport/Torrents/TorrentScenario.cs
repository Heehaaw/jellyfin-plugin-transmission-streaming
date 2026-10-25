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

    public async Task AddAsync(byte[] media, string fileName, CancellationToken cancellationToken, bool completed = false)
    {
        var files = await TorrentWorkspace.PrepareAsync(Workspace, media, fileName, cancellationToken, RemoteRoot);
        Hash = files.Metainfo.Hash;
        FileName = fileName;
        ExpectedBytes = media;
        if (completed)
        {
            // Import-only cases need a real verified torrent, without repeating the peer-transfer scenarios.
            await File.WriteAllBytesAsync(LocalDownloadPath, media, cancellationToken);
        }

        await services.ControlAsync(
            operation: "scenario",
            value: new
            {
                directory = services.RemotePath(files.SeedRoot)
            },
            cancellationToken: cancellationToken
        );
        await services.Rpc.CallAsync(
            method: "session_set",
            parameters: new
            {
                incomplete_dir = RemoteRoot + "/" + SwarmSettings.IncompleteDirectory
            },
            cancellationToken: cancellationToken
        );
        await services.Rpc.CallAsync(
            method: "torrent_add",
            parameters: new
            {
                metainfo = Convert.ToBase64String(files.Metainfo.Bytes),
                download_dir = RemoteRoot + "/" + SwarmSettings.CompletedDirectory,
                paused = completed
            },
            cancellationToken: cancellationToken
        );
        if (completed)
        {
            await VerifyCompleteAsync(cancellationToken);
        }
    }

    private async Task VerifyCompleteAsync(CancellationToken cancellationToken)
    {
        var target = new
        {
            ids = new[]
            {
                Hash
            }
        };
        await services.Rpc.CallAsync("torrent_verify", target, cancellationToken);
        await Poll.UntilAsync(() => ProgressAsync(cancellationToken), state => state.Complete && !state.Checking, "Transmission to verify prepared media", cancellationToken);
        await services.Rpc.CallAsync("torrent_start", target, cancellationToken);
    }

    public async Task AllowFirstPieceAsync(CancellationToken cancellationToken)
    {
        await ControlAsync("first", new { }, cancellationToken);
        await Poll.UntilAsync(() => ProgressAsync(cancellationToken), state => state.FirstPieceAvailable, "Transmission to verify the first seeded piece", cancellationToken);
    }

    public async Task AllowAllPiecesAsync(CancellationToken cancellationToken)
    {
        await ControlAsync("all", new { }, cancellationToken);
        await Poll.UntilAsync(
            probe: () => ProgressAsync(cancellationToken),
            ready: state => state.Complete && File.Exists(LocalDownloadPath),
            description: "Transmission to complete and move the synthetic download",
            cancellationToken: cancellationToken
        );
    }

    public async Task<TorrentProgress> ProgressAsync(CancellationToken cancellationToken)
    {
        var result = await services.Rpc.CallAsync(
            method: "torrent_get",
            parameters: new
            {
                ids = new[]
                {
                    Hash
                },
                fields = new[]
                {
                    "percent_done", "pieces", "download_dir", "sequential_download", "error_string", "status"
                }
            },
            cancellationToken: cancellationToken
        );
        var torrent = result.GetProperty("torrents").EnumerateArray().Single();
        return new TorrentProgress(
            PercentDone: torrent.GetProperty("percent_done").GetDouble(),
            Pieces: Convert.FromBase64String(torrent.GetProperty("pieces").GetString()!),
            DownloadDirectory: torrent.GetProperty("download_dir").GetString()!,
            SequentialDownload: torrent.GetProperty("sequential_download").GetBoolean(),
            Error: torrent.GetProperty("error_string").GetString()!,
            Status: torrent.GetProperty("status").GetInt32()
        );
    }

    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await services.Rpc.CallAsync(
            method: "torrent_remove",
            parameters: new
            {
                ids = new[]
                {
                    Hash
                },
                delete_local_data = false
            },
            cancellationToken: cancellationToken
        );
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
