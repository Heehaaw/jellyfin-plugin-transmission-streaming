namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using global::TransmissionStreaming.TestSupport.Torrents;
using System.Text.Json.Nodes;

/// <summary>Keeps a Jellyfin process alive across serialized scenarios and resets their mutable state through supported boundaries.</summary>
internal sealed class JellyfinHost : IAsyncDisposable
{
    private JellyfinContainer? server;
    private string administrator = "";
    private JsonNode defaults = null!;
    private bool failed;

    internal byte[] ReferenceVideo { get; private set; } = [];

    internal async Task<JellyfinContainer> GetOrStartAsync(DownloadServices containers, JellyfinBaseline baseline, CancellationToken cancellationToken)
    {
        if (failed)
        {
            throw new InvalidOperationException("The shared Jellyfin host failed to reset; refusing to reuse contaminated state.");
        }

        if (server is not null)
        {
            return server;
        }

        var workspace = Path.Combine(containers.Workspace, "jellyfin-host");
        baseline.Populate(Path.Combine(workspace, "jellyfin"));
        server = new JellyfinContainer(workspace, containers.Network, true, containers.Workspace);
        try
        {
            await server.StartAsync(cancellationToken);
            using var api = new JellyfinApi(server.Address);
            administrator = await api.AuthenticateAsync();
            await api.RequestAsync("/TransmissionStreaming/status");
            defaults = (await api.RequestAsync(ConfigurationPath))!.DeepClone();
            await api.RequestAsync("/ApplicationTestHost/state", method: HttpMethod.Delete);
            ReferenceVideo = await server.CreateVideoAsync(cancellationToken);
            return server;
        }
        catch
        {
            failed = true;
            throw;
        }
    }

    internal async Task ResetAsync(JellyfinApi api, CancellationToken cancellationToken)
    {
        try
        {
            var disabled = (await api.RequestAsync(ConfigurationPath))!.AsObject();
            disabled["Enabled"] = false;
            await api.RequestAsync(ConfigurationPath, disabled);
            // Disabling prevents new polls; diagnostics drain any work that had already started.
            await TestWait.UntilAsync(
                probe: async () =>
                {
                    var status = await api.RequestAsync("/TransmissionStreaming/status");
                    return !JellyfinApi.Value<bool>(status, "IsPolling") &&
                           JellyfinApi.Field(status, "Entries")!.AsArray().All(entry => JellyfinApi.Value<int>(entry, "Readers") == 0);
                },
                description: "disabled discovery and playback readers to drain"
            ).WaitAsync(cancellationToken);
            await ClearLibraryAsync(api);
            await ClearViewersAsync(api);
            await api.RequestAsync("/ApplicationTestHost/state", method: HttpMethod.Delete);
            await api.RequestAsync(ConfigurationPath, defaults.DeepClone());
            await VerifyEmptyAsync(api);
        }
        catch
        {
            failed = true;
            // A failed reset must not leave a live process accessing the scenario files being removed.
            await DisposeAsync();
            throw;
        }
    }

    private static string ConfigurationPath => $"/Plugins/{PluginBundle.BuildOutput.Id}/Configuration";

    private static async Task ClearLibraryAsync(JellyfinApi api)
    {
        await new JellyfinLibraryScan(api).WaitForIdleAsync();
        var libraries = (await api.RequestAsync("/Library/VirtualFolders"))!.AsArray();
        foreach (var library in libraries)
        {
            var name = Uri.EscapeDataString(JellyfinApi.Value<string>(library, "Name"));
            await api.RequestAsync($"/Library/VirtualFolders?name={name}&refreshLibrary=true", method: HttpMethod.Delete);
        }

        await TestWait.UntilAsync(
            probe: async () =>
            {
                var items = await api.RequestAsync("/Items?Recursive=true&IncludeItemTypes=Movie");
                return JellyfinApi.Field(items, "Items")!.AsArray().Count == 0;
            },
            description: "scenario movies to leave the live library"
        );
        await new JellyfinLibraryScan(api).WaitForIdleAsync();
    }

    private async Task ClearViewersAsync(JellyfinApi api)
    {
        var users = (await api.RequestAsync("/Users"))!.AsArray();
        foreach (var user in users)
        {
            var id = JellyfinApi.Value<string>(user, "Id");
            if (id != administrator)
            {
                await api.RequestAsync("/Users/" + id, method: HttpMethod.Delete);
            }
        }
    }

    private static async Task VerifyEmptyAsync(JellyfinApi api)
    {
        var status = await api.RequestAsync("/TransmissionStreaming/status");
        var libraries = (await api.RequestAsync("/Library/VirtualFolders"))!.AsArray();
        var users = (await api.RequestAsync("/Users"))!.AsArray();
        if (JellyfinApi.Value<bool>(status, "Enabled") || JellyfinApi.Field(status, "Entries")!.AsArray().Count != 0 ||
            JellyfinApi.Field(status, "Completed")!.AsArray().Count != 0 || libraries.Count != 0 || users.Count != 1)
        {
            var enabled = JellyfinApi.Value<bool>(status, "Enabled");
            var entries = JellyfinApi.Field(status, "Entries")!.AsArray().Count;
            var completed = JellyfinApi.Field(status, "Completed")!.AsArray().Count;
            throw new InvalidOperationException(
                $"The live Jellyfin reset left state: enabled={enabled}, entries={entries}, completed={completed}, libraries={libraries.Count}, users={users.Count}."
            );
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (server is not null)
        {
            await server.DisposeAsync();
            server = null;
        }
    }
}
