namespace TransmissionStreaming.TestSupport;

using System.Diagnostics;

internal static class Poll
{
    public static async Task<T> UntilAsync<T>(Func<Task<T>> probe, Func<T, bool> ready, string description, CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(45));
        var watch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            T value;
            try
            {
                value = await probe().WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for {description} after {watch.Elapsed}.");
            }
            if (ready(value))
            {
                return value;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for {description} after {watch.Elapsed}.");
            }
        }
    }
}
