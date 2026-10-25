namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Discovery;

using Microsoft.Extensions.Time.Testing;
using System.Threading.Channels;

/// <summary>Lets tests advance virtual time only after the worker has registered its next wait.</summary>
internal sealed class ObservableFakeTimeProvider : FakeTimeProvider
{
    private readonly Channel<TimeSpan> delays = Channel.CreateUnbounded<TimeSpan>();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        delays.Writer.TryWrite(dueTime);
        return timer;
    }

    public Task<TimeSpan> NextDelayAsync()
    {
        // The guard fails a broken synchronization contract; it does not drive playback time.
        return delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
