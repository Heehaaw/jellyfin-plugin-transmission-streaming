namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Hosting;
using System.Text.Json.Nodes;
using Xunit;

public sealed class DuplicateReconciliationTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_MergeAnIndexedNativeDuplicateWithoutDeletingMedia(WatchState playedViewer, WatchState resumingViewer)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        playedViewer.Played = true;
        playedViewer.IsFavorite = true;
        playedViewer.PlaybackPositionTicks = 0;
        resumingViewer.Played = false;
        resumingViewer.IsFavorite = false;
        var maximumResumeSeconds = (long)(JellyfinContainer.MediaDuration.TotalSeconds / 2);
        var resumeSeconds = resumingViewer.PlaybackPositionTicks % maximumResumeSeconds + 1;
        resumingViewer.PlaybackPositionTicks = resumeSeconds * TimeSpan.TicksPerSecond;
        var states = new[]
        {
            playedViewer, resumingViewer
        };
        var permanentUrl = await sut.WaitForStreamAsync();
        await sut.StageImportedFileAsync();
        // Index the physical import before arr publishes its history. This reproduces the scan race.
        await sut.AddLibraryAsync("Native import", sut.ImportedMoviesPath);
        var nativeId = await sut.WaitForItemAsync(false);
        var viewer = await sut.Api.RequestAsync(
            path: "/Users/New",
            body: new
            {
                Name = "native-viewer",
                Password = "Disposable-native-viewer-only-123!"
            }
        );
        var users = new[]
        {
            sut.Administrator, JellyfinApi.Value<string>(viewer, "Id")
        };
        var before = new Dictionary<string, JsonNode?>();
        foreach (var (user, state) in users.Zip(states))
        {
            await sut.Api.RequestAsync($"/UserItems/{nativeId}/UserData?userId={user}", state);
            before[user] = await sut.Api.RequestAsync($"/UserItems/{nativeId}/UserData?userId={user}");
        }

        try
        {
            // Act
            await sut.AddLibraryAsync("Streaming", sut.ProjectionMoviesPath);
            var projectedId = await sut.WaitForItemAsync();
            await sut.ImportAsync();
            await JellyfinTestServer.UntilAsync(async () => await sut.FindItemAsync(false) is null, "native duplicate reconciliation");
            var after = new Dictionary<string, JsonNode?>();
            foreach (var user in users)
            {
                after[user] = await sut.Api.RequestAsync($"/UserItems/{projectedId}/UserData?userId={user}");
            }

            await sut.RestartAsync();
            await sut.Api.RequestAsync(
                path: "/Library/Refresh",
                body: new
                    { }
            );
            var restartedId = await sut.WaitForItemAsync();
            var persisted = new Dictionary<string, JsonNode?>();
            foreach (var user in users)
            {
                persisted[user] = await sut.Api.RequestAsync($"/UserItems/{projectedId}/UserData?userId={user}");
            }

            using var client = new HttpClient();
            var playback = await client.GetByteArrayAsync(sut.HostStream(permanentUrl), cancellationToken);

            // Assert
            projectedId.Should().NotBe(nativeId);
            restartedId.Should().Be(projectedId);
            File.Exists(Path.Combine(sut.Workspace, "movies", sut.Swarm.FileName)).Should().BeTrue();
            playback.Should().Equal(sut.ExpectedBytes);
            (await sut.FindItemAsync(false)).Should().BeNull();
            foreach (var user in users)
            {
                foreach (var field in new[]
                         {
                             "Played", "PlaybackPositionTicks", "PlayCount", "IsFavorite"
                         })
                {
                    var expected = JellyfinApi.Field(before[user], field)?.ToJsonString();
                    var actual = JellyfinApi.Field(after[user], field)?.ToJsonString();
                    var afterRestart = JellyfinApi.Field(persisted[user], field)?.ToJsonString();
                    afterRestart.Should().Be(expected, $"{field} must remain persisted after duplicate reconciliation and restart");
                    actual.Should().Be(expected, $"{field} must be visible immediately after duplicate reconciliation");
                }
            }
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture();
            fixture.Customize(new AutoMoqCustomization());
            fixture.Customize<WatchState>(composer => composer.With(value => value.Rating, () => fixture.Create<int>() % 11d));
            return fixture;
        }
    );
}
