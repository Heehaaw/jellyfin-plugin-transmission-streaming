namespace TransmissionStreaming.TestSupport.Torrents;

using DotNet.Testcontainers.Containers;
using System.Text.RegularExpressions;

internal static class ContainerDiagnostics
{
    public static async Task<string> ReadAsync(IContainer daemon, IContainer seed, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var daemonLogs = await ReadLogsAsync(daemon, deadline.Token).ConfigureAwait(false);
        var seedLogs = await ReadLogsAsync(seed, deadline.Token).ConfigureAwait(false);
        // Future fixture changes must not leak playback capabilities into failure output.
        return Regex.Replace("Transmission:\n" + daemonLogs + "\nControlled seed:\n" + seedLogs, "(?i)(key=)[^&\\s\\\"<>]+", "$1[redacted]");
    }

    private static async Task<string> ReadLogsAsync(IContainer container, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(container.Id))
        {
            return "Container was not created.";
        }
        try
        {
            var logs = await container.GetLogsAsync(ct: cancellationToken).ConfigureAwait(false);
            return logs.Stdout + logs.Stderr;
        }
        catch (Exception exception)
        {
            // Diagnostics must not hide the original startup or assertion failure.
            return $"Container logs unavailable ({exception.GetType().Name}).";
        }
    }
}
