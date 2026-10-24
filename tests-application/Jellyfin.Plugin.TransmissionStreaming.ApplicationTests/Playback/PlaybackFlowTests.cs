namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Hosting;
using Xunit;

/// <summary>Exercises the public plugin and Jellyfin APIs with real, locally downloaded torrent bytes.</summary>
public sealed class PlaybackFlowTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_KeepPlaybackAndUserStateThroughImportAndRestart(WatchState playedViewer, WatchState resumingViewer)
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
        var states = new[] { playedViewer, resumingViewer };
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var permanentUrl = await sut.WaitForStreamAsync();
        var hostUrl = sut.HostStream(permanentUrl);

        try
        {
            // Act
            // The library entry must be usable while the seed still withholds the remaining pieces.
            await sut.AddLibraryAsync("Streaming", sut.ProjectionMoviesPath);
            var itemId = await sut.WaitForItemAsync();
            var indexingDownload = await sut.Swarm.ProgressAsync(cancellationToken);
            var playbackInfo = await sut.Api.RequestAsync($"/Items/{itemId}/PlaybackInfo?userId={sut.Administrator}");
            var sources = JellyfinApi.Field(playbackInfo, "MediaSources")!.AsArray();
            var advertisedPath = JellyfinApi.Value<string>(sources[0], "Path");

            // Probe requests use the short scanner deadline; sustained playback allows a real peer scheduling pulse.
            await sut.ConfigureAsync(waitSeconds: 30);
            var playback = await ProgressivePlayback.CaptureAsync(sut, client, hostUrl, cancellationToken);
            var history = await ViewerHistory.SaveAsync(sut, itemId, states);

            await sut.ImportAsync();
            using var afterImport = await client.GetAsync(hostUrl, cancellationToken);
            var importedBytes = await afterImport.Content.ReadAsByteArrayAsync(cancellationToken);
            var afterItem = await sut.FindItemAsync(true);
            await sut.RestartAsync();
            using var afterRestart = await client.GetAsync(sut.HostStream(permanentUrl), cancellationToken);
            var restartedBytes = await afterRestart.Content.ReadAsByteArrayAsync(cancellationToken);
            var restartedId = await sut.WaitForItemAsync();
            var retainedUrl = await sut.WaitForStreamAsync();
            // Assert
            indexingDownload.Complete.Should().BeFalse("Jellyfin must advertise the projected item before the download finishes");
            (advertisedPath == permanentUrl).Should().BeTrue("Jellyfin playback must resolve the permanent stream link");
            playback.ShouldMatch(sut.ExpectedBytes, sut.Swarm.PieceSize);
            importedBytes.Should().Equal(sut.ExpectedBytes);
            restartedBytes.Should().Equal(sut.ExpectedBytes);
            JellyfinApi.Value<string>(afterItem, "Id").Should().Be(itemId);
            restartedId.Should().Be(itemId);
            (retainedUrl == permanentUrl).Should().BeTrue("the permanent capability must survive import and restart");
            await history.ShouldBeRetainedAsync(sut.Api);
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
