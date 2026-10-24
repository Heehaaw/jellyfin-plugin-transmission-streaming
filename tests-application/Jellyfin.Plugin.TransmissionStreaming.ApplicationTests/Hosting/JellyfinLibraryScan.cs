namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using System.Net;
using System.Text.Json.Nodes;

/// <summary>Starts a scan after folder creation, rather than joining an older scan whose root list is already fixed.</summary>
internal sealed class JellyfinLibraryScan(JellyfinApi api)
{
    private const string TaskKey = "RefreshLibrary";
    private const string Idle = "Idle";
    private const string Running = "Running";

    public async Task StartAfterFolderChangeAsync()
    {
        ScanSnapshot? idle = null;
        await TestWait.UntilAsync(async () =>
        {
            idle = await ReadAsync();
            return idle.State == Idle;
        }, "the previous library scan to finish");

        try
        {
            await api.RequestAsync($"/ScheduledTasks/Running/{idle!.Id}", new { });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // Another scan won after we observed Idle. It started after the folder existed, so it includes that folder.
            var winner = await ReadAsync();
            if (!winner.StartedAfter(idle!))
            {
                throw;
            }
            return;
        }

        // A tiny scan can finish between polls. Its execution timestamp still proves a new run was started.
        await TestWait.UntilAsync(async () => (await ReadAsync()).StartedAfter(idle!), "a post-folder library scan to start");
    }

    private async Task<ScanSnapshot> ReadAsync()
    {
        var tasks = await api.RequestAsync("/ScheduledTasks");
        var task = tasks!.AsArray().Single(item => JellyfinApi.Value<string>(item, "Key") == TaskKey);
        var result = JellyfinApi.Field(task, "LastExecutionResult");
        var started = JellyfinApi.Field(result, "StartTimeUtc")?.GetValue<string>();
        return new ScanSnapshot(JellyfinApi.Value<string>(task, "Id"), JellyfinApi.Value<string>(task, "State"), started);
    }

    private sealed record ScanSnapshot(string Id, string State, string? LastStarted)
    {
        public bool StartedAfter(ScanSnapshot previous)
        {
            return State == Running || LastStarted is not null && LastStarted != previous.LastStarted;
        }
    }
}
