namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using FluentAssertions;
using Hosting;
using System.Text.Json.Nodes;

/// <summary>Owns the two-viewer setup and compares persisted playback fields across the handoff.</summary>
internal sealed class ViewerHistory(string itemId, IReadOnlyDictionary<string, JsonNode?> before)
{
    private static readonly string[] Fields = ["Played", "PlaybackPositionTicks", "PlayCount", "IsFavorite", "LastPlayedDate", "Rating", "Likes"];

    public static async Task<ViewerHistory> SaveAsync(JellyfinTestServer server, string itemId, WatchState[] states)
    {
        var viewer = await server.Api.RequestAsync("/Users/New", new { Name = "second-viewer", Password = "Disposable-viewer-only-123!" });
        var secondUser = JellyfinApi.Value<string>(viewer, "Id");
        var users = new[] { server.Administrator, secondUser };
        var before = new Dictionary<string, JsonNode?>();
        foreach (var (user, state) in users.Zip(states))
        {
            await server.Api.RequestAsync($"/UserItems/{itemId}/UserData?userId={user}", state);
            before[user] = await server.Api.RequestAsync($"/UserItems/{itemId}/UserData?userId={user}");
        }

        return new ViewerHistory(itemId, before);
    }

    public async Task ShouldBeRetainedAsync(JellyfinApi api, string? targetItemId = null)
    {
        foreach (var (user, original) in before)
        {
            var current = await api.RequestAsync($"/UserItems/{targetItemId ?? itemId}/UserData?userId={user}");
            foreach (var field in Fields)
            {
                JsonNode.DeepEquals(JellyfinApi.Field(current, field), JellyfinApi.Field(original, field))
                    .Should().BeTrue($"{field} belongs to each viewer");
            }
        }
    }
}
