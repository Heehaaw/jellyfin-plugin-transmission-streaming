namespace TransmissionStreaming.TestSupport.Torrents.Seed;

internal sealed record SeedConfiguration(string Hash, int PieceSize, string FileName, string Title, int HttpPort, int PeerPort, string Host, string ArrKey, string CompletedRoot);
internal sealed record SeedState(int Allowed, string? ImportedPath, bool Failure, string HistoryHash, bool Episode, bool Removed = false);

internal sealed class SeedScenario : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private int disposed;

    public SeedScenario(SeedConfiguration configuration, byte[] media)
    {
        Configuration = configuration;
        Media = media;
        State = new SeedState(0, null, false, configuration.Hash, false);
        CancellationToken = lifetime.Token;
    }

    public object Gate { get; } = new();
    public string? PeerFailure { get; set; }
    public SeedConfiguration Configuration { get; }
    public byte[] Media { get; }
    public int PieceCount => (Media.Length + Configuration.PieceSize - 1) / Configuration.PieceSize;
    public SeedState State { get; set; }
    public CancellationToken CancellationToken { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }
    }
}

internal sealed class SeedControl(SeedConfiguration configuration, byte[] media) : IDisposable
{
    private SeedScenario current = new(configuration, media);
    public SeedScenario Current => Volatile.Read(ref current);
    public string? CatalogRoot { get; set; }

    public void Reset(SeedConfiguration next, byte[] bytes)
    {
        // A peer keeps one immutable scenario. Reset cancels its reads before another torrent can borrow the server.
        Interlocked.Exchange(ref current, new SeedScenario(next, bytes)).Dispose();
        CatalogRoot = null;
    }

    public void Dispose()
    {
        Current.Dispose();
    }
}
