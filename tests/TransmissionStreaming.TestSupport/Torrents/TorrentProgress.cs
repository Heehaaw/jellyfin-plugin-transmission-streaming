namespace TransmissionStreaming.TestSupport.Torrents;

internal sealed record TorrentProgress(double PercentDone, byte[] Pieces, string DownloadDirectory, bool SequentialDownload, string Error)
{
    public bool FirstPieceAvailable => Pieces.Length > 0 && (Pieces[0] & 0x80) != 0;
    public bool Complete => PercentDone >= 1;
}
