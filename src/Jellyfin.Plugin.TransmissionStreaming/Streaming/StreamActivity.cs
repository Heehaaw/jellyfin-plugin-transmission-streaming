namespace Jellyfin.Plugin.TransmissionStreaming.Streaming;

using System.Collections.Concurrent;


/// <summary>Tracks outstanding reads for diagnostics without retaining video data.</summary>
public sealed class StreamActivity
{
    private readonly ConcurrentDictionary<string, int> readers = new(StringComparer.Ordinal);
    public int Count(string id) => readers.GetValueOrDefault(id);

    public IDisposable Open(string id)
    {
        readers.AddOrUpdate(id, 1, (_, count) => count + 1);
        return new Lease(() => readers.AddOrUpdate(id, 0, (_, count) => Math.Max(0, count - 1)));
    }

    private sealed class Lease(Action close) : IDisposable
    {
        private Action? release = close;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }
}
