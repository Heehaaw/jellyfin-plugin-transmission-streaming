namespace TransmissionStreaming.TestSupport.Torrents;

using DotNet.Testcontainers.Containers;
using System.Text.RegularExpressions;

internal static class ContainerDiagnostics
{
    private const int MaximumContainerLogLines = 28;
    private const int MaximumContainerLogCharacters = 6000;
    private const string SensitiveContent =
        @"(?i)\b(?:api[_ -]?key|access[_ -]?token|refresh[_ -]?token|token|password|passwd|credentials?|authorization|bearer|secret)\b" +
        @"|\bkey\s*=|https?://[^/\s]+@";

    public static async Task<string> ReadAsync(IContainer? daemon, IContainer? seed, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        // Read both independently so an unresponsive Docker log request cannot consume the other's diagnostic budget.
        var daemonLogs = ReadLogsAsync(daemon, deadline.Token);
        var seedLogs = ReadLogsAsync(seed, deadline.Token);
        await Task.WhenAll(daemonLogs, seedLogs).ConfigureAwait(false);
        return "Transmission:\n" + await daemonLogs.ConfigureAwait(false) + "\nControlled seed:\n" + await seedLogs.ConfigureAwait(false);
    }

    private static async Task<string> ReadLogsAsync(IContainer? container, CancellationToken cancellationToken)
    {
        if (container is null)
        {
            return "Container was not constructed.";
        }

        var state = "State unavailable";
        try
        {
            state = "State: " + container.State;
            if (string.IsNullOrEmpty(container.Id))
            {
                return state + "; container was not created.";
            }

            var logs = await container.GetLogsAsync(ct: cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            var tail = SanitizedTail(logs.Stdout + "\n" + logs.Stderr);
            return state + "\n" + tail;
        }
        catch (Exception exception)
        {
            // Exception messages can contain Docker addresses or credentials; retain only the failure type.
            return $"{state}; container logs unavailable ({exception.GetType().Name}).";
        }
    }

    private static string SanitizedTail(string logs)
    {
        var lines = logs.Split('\n').TakeLast(MaximumContainerLogLines).Select(line =>
            Regex.IsMatch(line, SensitiveContent, RegexOptions.None, TimeSpan.FromMilliseconds(100))
                ? "[redacted credential-bearing log line]"
                : line
        );
        var tail = string.Join('\n', lines);
        // Sanitize before truncating so a marker outside the retained characters cannot expose its value.
        return tail.Length > MaximumContainerLogCharacters ? tail[^MaximumContainerLogCharacters..] : tail;
    }
}
