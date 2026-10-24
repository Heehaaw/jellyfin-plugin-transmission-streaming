namespace TransmissionStreaming.Packaging;

internal static class ScriptHost
{
    public static async Task<int> RunAsync(Func<CancellationToken, Task> operation)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            await operation(cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Cancelled.");
            return 1;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or HttpRequestException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
