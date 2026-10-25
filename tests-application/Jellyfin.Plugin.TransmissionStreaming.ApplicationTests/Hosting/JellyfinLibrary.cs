namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using System.Text.Json.Nodes;

/// <summary>Drives indexing through Jellyfin's API with internet metadata and automatic extraction disabled.</summary>
internal sealed class JellyfinLibrary(JellyfinApi api, string administrator, string sourceName)
{
    public async Task AddAsync(string name, string path)
    {
        var options = new
        {
            EnableRealtimeMonitor = false,
            EnableInternetProviders = false,
            EnableAutomaticSeriesGrouping = false,
            SaveLocalMetadata = false,
            AutomaticallyRefreshMetadataAfterScan = false,
            EnableChapterImageExtraction = false,
            EnableTrickplayImageExtraction = false,
            TypeOptions = new[]
            {
                new
                {
                    Type = "Movie",
                    MetadataFetchers = Array.Empty<string>(),
                    ImageFetchers = Array.Empty<string>()
                }
            }
        };
        var query = $"name={Uri.EscapeDataString(name)}&collectionType=movies&refreshLibrary=false&paths={Uri.EscapeDataString(path)}";
        // A refresh requested during an existing scan can be dropped before the new root is included.
        await api.RequestAsync(
            path: $"/Library/VirtualFolders?{query}",
            body: new
            {
                LibraryOptions = options
            }
        );
        await new JellyfinLibraryScan(api).StartAfterFolderChangeAsync();
    }

    public async Task<JsonNode?> FindAsync(bool projected)
    {
        var result = await api.RequestAsync($"/Items?userId={administrator}&Recursive=true&IncludeItemTypes=Movie&Fields=Path");
        var items = JellyfinApi.Field(result, "Items")!.AsArray();
        return items.FirstOrDefault(item =>
            {
                var path = JellyfinApi.Field(item, "Path")?.GetValue<string>() ?? "";
                return projected ? path.EndsWith(".strm", StringComparison.Ordinal) : path.EndsWith(sourceName, StringComparison.Ordinal);
            }
        );
    }

    public async Task<string> WaitForItemAsync(bool projected = true)
    {
        JsonNode? item = null;
        await TestWait.UntilAsync(async () => (item = await FindAsync(projected)) is not null, "Jellyfin item indexing");
        return JellyfinApi.Value<string>(item, "Id");
    }
}
