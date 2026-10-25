namespace HdrPlayback.Infrastructure.Playback;

using Core.Playback;
using System.Collections.Concurrent;

internal sealed class HdrSessionStore(TimeProvider timeProvider) : IHdrSessionStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(6);
    private readonly ConcurrentDictionary<HdrSessionKey, long> sessions = new();

    public void Enable(HdrSessionKey key)
    {
        foreach (var entry in sessions)
        {
            if (timeProvider.GetElapsedTime(entry.Value) >= Retention)
            {
                sessions.TryRemove(entry.Key, out _);
            }
        }

        sessions[key] = timeProvider.GetTimestamp();
    }

    public bool Contains(HdrSessionKey key)
    {
        if (!sessions.TryGetValue(key, out var timestamp))
        {
            return false;
        }

        if (timeProvider.GetElapsedTime(timestamp) >= Retention)
        {
            sessions.TryRemove(key, out _);
            return false;
        }

        // Repeated lookups refresh active sessions; retention only bounds abandoned negotiations.
        sessions[key] = timeProvider.GetTimestamp();
        return true;
    }

    public void Remove(HdrSessionKey key)
    {
        sessions.TryRemove(key, out _);
    }
}
