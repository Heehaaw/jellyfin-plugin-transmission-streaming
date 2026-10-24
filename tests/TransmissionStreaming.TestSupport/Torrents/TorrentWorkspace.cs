namespace TransmissionStreaming.TestSupport.Torrents;

using System.Text.Json;

internal sealed record TorrentWorkspace(string SeedRoot, TorrentMetainfo Metainfo)
{
    public static async Task<TorrentWorkspace> PrepareAsync(string workspace, byte[] media, string fileName, CancellationToken cancellationToken)
    {
        if (media.Length <= SwarmSettings.PieceSize * 2 || Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("Synthetic media needs at least three pieces and a plain filename.");
        }

        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(workspace, SwarmSettings.IncompleteDirectory));
        Directory.CreateDirectory(Path.Combine(workspace, SwarmSettings.CompletedDirectory));
        Directory.CreateDirectory(Path.Combine(workspace, "movies"));
        var seedRoot = Directory.CreateDirectory(Path.Combine(workspace, ".seed")).FullName;
        var settingsRoot = Directory.CreateDirectory(Path.Combine(workspace, ".transmission")).FullName;
        var metainfo = TorrentMetainfo.Create(media, fileName, SwarmSettings.PieceSize);
        await File.WriteAllBytesAsync(Path.Combine(seedRoot, "reference.mkv"), media, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(seedRoot, "seed.json"), JsonSerializer.Serialize(new
        {
            hash = metainfo.Hash,
            pieceSize = SwarmSettings.PieceSize,
            fileName,
            title = Path.GetFileNameWithoutExtension(fileName),
            httpPort = SwarmSettings.HttpPort,
            peerPort = SwarmSettings.PeerPort,
            host = SwarmSettings.SeedHost,
            arrKey = SwarmSettings.ArrKey,
            completedRoot = SwarmSettings.CompletedRoot
        }), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(settingsRoot, "settings.json"), JsonSerializer.Serialize(DaemonConfiguration()), cancellationToken).ConfigureAwait(false);
        return new TorrentWorkspace(seedRoot, metainfo);
    }

    // Playback must enable sequential mode against a real nonzero application cache.
    private static object DaemonConfiguration()
    {
        return new
        {
            rpc_authentication_required = false,
            rpc_whitelist_enabled = false,
            rpc_host_whitelist_enabled = false,
            rpc_bind_address = "0.0.0.0",
            dht_enabled = false,
            pex_enabled = false,
            lpd_enabled = false,
            utp_enabled = false,
            port_forwarding_enabled = false,
            default_trackers = "",
            download_dir = SwarmSettings.CompletedRoot,
            incomplete_dir = SwarmSettings.IncompleteRoot,
            incomplete_dir_enabled = true,
            rename_partial_files = true,
            sequential_download = false,
            cache_size_mib = 4,
            preallocation = 1,
            encryption = "tolerated",
            message_level = 2
        };
    }
}
