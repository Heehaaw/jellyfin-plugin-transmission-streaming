namespace TransmissionStreaming.Core.Transmission;

using System.Text.Json.Serialization;


/// <summary>Connection settings for a Transmission RPC endpoint.</summary>
public sealed record TransmissionConnection(string Url, string Username = "", string Password = "")
{
    // Records otherwise include their property values in log messages and assertion output.
    public override string ToString() => "Transmission RPC connection (credentials redacted)";
}

/// <summary>A torrent's downloaded-piece map and file layout.</summary>
public sealed record TorrentSnapshot
{
    [JsonPropertyName("hash_string")] public string Hash { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonPropertyName("download_dir")] public string DownloadDirectory { get; set; } = "";
    [JsonPropertyName("piece_count")] public int PieceCount { get; set; }
    [JsonPropertyName("piece_size")] public long PieceSize { get; set; }
    public string Pieces { get; set; } = "";
    public List<TorrentFileSnapshot> Files { get; set; } = [];
    [JsonPropertyName("percent_done")] public double PercentDone { get; set; }
    public int Status { get; set; }
    [JsonPropertyName("error_string")] public string ErrorString { get; set; } = "";
    [JsonPropertyName("sequential_download")] public bool SequentialDownload { get; set; }
    [JsonPropertyName("sequential_download_from_piece")] public int SequentialDownloadFromPiece { get; set; }

    /// <summary>Returns the file's global byte offset in the torrent.</summary>
    public long GetFileOffset(int fileIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileIndex);
        if (fileIndex >= Files.Count) throw new ArgumentOutOfRangeException(nameof(fileIndex));
        long offset = 0;
        for (var i = 0; i < fileIndex; i++) offset = checked(offset + Files[i].Length);
        return offset;
    }
}

/// <summary>A file in torrent layout order.</summary>
public sealed record TorrentFileSnapshot
{
    public string Name { get; set; } = "";
    public long Length { get; set; }
    [JsonPropertyName("bytes_completed")] public long BytesCompleted { get; set; }
}

/// <summary>Session properties needed to locate files and assess write-cache safety.</summary>
public sealed record TransmissionSessionSnapshot
{
    [JsonPropertyName("incomplete_dir")] public string IncompleteDirectory { get; set; } = "";
    [JsonPropertyName("incomplete_dir_enabled")] public bool IncompleteDirectoryEnabled { get; set; }
    [JsonPropertyName("rename_partial_files")] public bool RenamePartialFiles { get; set; }
    [JsonPropertyName("cache_size_mib")] public long? CacheSizeMiB { get; set; }
    public string Version { get; set; } = "";
}
