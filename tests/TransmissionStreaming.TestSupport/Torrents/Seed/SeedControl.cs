namespace TransmissionStreaming.TestSupport.Torrents.Seed;

internal sealed record SeedConfiguration(string Hash, int PieceSize, string FileName, string Title, int HttpPort, int PeerPort, string Host, string ArrKey, string CompletedRoot);
internal sealed record SeedState(int Allowed, string? ImportedPath, bool Failure, string HistoryHash, bool Episode);
internal sealed class SeedControl(SeedConfiguration configuration, byte[] media)
{
    public object Gate { get; } = new();
    public SeedConfiguration Configuration { get; } = configuration;
    public byte[] Media { get; } = media;
    public int PieceCount => (Media.Length + Configuration.PieceSize - 1) / Configuration.PieceSize;
    public SeedState State { get; set; } = new(0, null, false, configuration.Hash, false);
}
