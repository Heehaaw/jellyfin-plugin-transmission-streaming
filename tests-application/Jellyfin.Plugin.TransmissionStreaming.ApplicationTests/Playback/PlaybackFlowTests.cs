namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

/// <summary>Exercises the public plugin and Jellyfin APIs with real, locally downloaded torrent bytes.</summary>
public sealed class PlaybackFlowTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_KeepPlaybackAndUserStateThroughImportAndRestart(WatchState playedViewer, WatchState resumingViewer)
    {
        // Arrange
        playedViewer.Played = true;
        playedViewer.IsFavorite = true;
        playedViewer.PlaybackPositionTicks = 0;
        resumingViewer.Played = false;
        resumingViewer.IsFavorite = false;
        var maximumResumeSeconds = (long)(JellyfinContainer.MediaDuration.TotalSeconds / 2);
        var resumeSeconds = resumingViewer.PlaybackPositionTicks % maximumResumeSeconds + 1;
        resumingViewer.PlaybackPositionTicks = resumeSeconds * TimeSpan.TicksPerSecond;
        var states = new[] { playedViewer, resumingViewer };
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var permanentUrl = await sut.WaitForStreamAsync();
        var hostUrl = sut.HostStream(permanentUrl);
        using var available = new HttpRequestMessage(HttpMethod.Get, hostUrl);
        available.Headers.Range = new RangeHeaderValue(0, sut.Swarm.PieceSize - 1);

        try
        {
            // Act
            // The library entry must be usable while the seed still withholds the remaining pieces.
            await sut.AddLibraryAsync("Streaming", "/data/projection/movies");
            var itemId = await sut.WaitForItemAsync();
            var indexingDownload = await sut.Swarm.ProgressAsync(CancellationToken.None);
            var playbackInfo = await sut.Api.RequestAsync($"/Items/{itemId}/PlaybackInfo?userId={sut.Administrator}");
            var sources = JellyfinApi.Field(playbackInfo, "MediaSources")!.AsArray();
            var advertisedPath = JellyfinApi.Value<string>(sources[0], "Path");

            using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, hostUrl));
            using var range = await client.SendAsync(available);
            var firstPiece = await range.Content.ReadAsByteArrayAsync();
            using var playback = await client.GetAsync(hostUrl, HttpCompletionOption.ResponseHeadersRead);
            await using var body = await playback.Content.ReadAsStreamAsync();
            var prefix = new byte[sut.Swarm.PieceSize];
            await body.ReadExactlyAsync(prefix);
            var nextByte = new byte[1];
            var pendingRead = body.ReadAsync(nextByte).AsTask();
            var waitedForPieces = await Task.WhenAny(pendingRead, Task.Delay(700)) != pendingRead;
            await sut.Swarm.AllowAllPiecesAsync(CancellationToken.None);
            var firstAfterWait = await pendingRead.WaitAsync(TimeSpan.FromSeconds(30));
            await using var received = new MemoryStream();
            await received.WriteAsync(prefix);
            await received.WriteAsync(nextByte.AsMemory(0, firstAfterWait));
            await body.CopyToAsync(received);

            var viewer = await sut.Api.RequestAsync("/Users/New", new { Name = "second-viewer", Password = "Disposable-viewer-only-123!" });
            var secondUser = JellyfinApi.Value<string>(viewer, "Id");
            var users = new[] { sut.Administrator, secondUser };
            var before = new Dictionary<string, JsonNode?>();
            foreach (var (user, state) in users.Zip(states))
            {
                await sut.Api.RequestAsync($"/UserItems/{itemId}/UserData?userId={user}", state);
                before[user] = await sut.Api.RequestAsync($"/UserItems/{itemId}/UserData?userId={user}");
            }

            await sut.ImportAsync();
            using var afterImport = await client.GetAsync(hostUrl);
            var importedBytes = await afterImport.Content.ReadAsByteArrayAsync();
            var afterItem = await sut.FindItemAsync(true);
            await sut.RestartAsync();
            using var afterRestart = await client.GetAsync(sut.HostStream(permanentUrl));
            var restartedBytes = await afterRestart.Content.ReadAsByteArrayAsync();
            var restartedId = await sut.WaitForItemAsync();
            var retainedUrl = await sut.WaitForStreamAsync();
            var after = new Dictionary<string, JsonNode?>();
            foreach (var user in users)
            {
                after[user] = await sut.Api.RequestAsync($"/UserItems/{itemId}/UserData?userId={user}");
            }

            // Assert
            indexingDownload.Complete.Should().BeFalse("Jellyfin must advertise the projected item before the download finishes");
            (advertisedPath == permanentUrl).Should().BeTrue("Jellyfin playback must resolve the permanent stream link");
            head.StatusCode.Should().Be(HttpStatusCode.OK);
            head.Content.Headers.ContentLength.Should().Be(sut.ExpectedBytes.Length);
            range.StatusCode.Should().Be(HttpStatusCode.PartialContent);
            range.Content.Headers.ContentRange!.Length.Should().Be(sut.ExpectedBytes.Length);
            firstPiece.Should().Equal(sut.ExpectedBytes.Take(sut.Swarm.PieceSize));
            waitedForPieces.Should().BeTrue("the local seed withheld every remaining piece");
            received.ToArray().Should().Equal(sut.ExpectedBytes);
            importedBytes.Should().Equal(sut.ExpectedBytes);
            restartedBytes.Should().Equal(sut.ExpectedBytes);
            JellyfinApi.Value<string>(afterItem, "Id").Should().Be(itemId);
            restartedId.Should().Be(itemId);
            (retainedUrl == permanentUrl).Should().BeTrue("the permanent capability must survive import and restart");
            foreach (var user in users)
            {
                foreach (var field in new[] { "Played", "PlaybackPositionTicks", "PlayCount", "IsFavorite" })
                {
                    JsonNode.DeepEquals(JellyfinApi.Field(after[user], field), JellyfinApi.Field(before[user], field)).Should().BeTrue($"{field} belongs to each viewer");
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
        return fixture;
    });
}

public sealed record WatchState
{
    public bool Played { get; set; }
    public long PlaybackPositionTicks { get; set; }
    public int PlayCount { get; set; }
    public bool IsFavorite { get; set; }
}
