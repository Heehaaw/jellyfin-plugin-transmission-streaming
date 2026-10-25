namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Hosting;
using System.Net;
using System.Text.Json.Nodes;
using Xunit;

/// <summary>Exercises progressive playback, seeding, and native handoff through actual Jellyfin APIs.</summary>
public sealed class PlaybackFlowTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_KeepPlaybackAndUserStateThroughNativeHandoffAndRestart(WatchState playedViewer, WatchState resumingViewer)
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
        var projectionId = new Uri(permanentUrl).Segments.Last();
        var hostUrl = sut.HostStream(permanentUrl);
        var activityAddress = new Uri(JellyfinContainer.InternalAddress, "web/#/configurationpage?name=transmission-streaming").AbsoluteUri;
        var generatedStream = Directory.EnumerateFiles(Path.Combine(sut.Workspace, "projection"), "*.strm", SearchOption.AllDirectories).Single();
        var generatedMetadata = Path.ChangeExtension(generatedStream, ".nfo");
        var importedFile = Path.Combine(sut.Workspace, "movies", sut.Swarm.FileName);

        try
        {
            // Act
            await sut.AddLibraryAsync("Streaming", sut.ProjectionMoviesPath);
            var projectedItemId = await sut.WaitForItemAsync();
            var downloadingOverview = await WaitForActivityLinkAsync(projectedItemId, activityAddress);
            var indexingDownload = await sut.Swarm.ProgressAsync(cancellationToken);
            var playbackInfo = await sut.Api.RequestAsync($"/Items/{projectedItemId}/PlaybackInfo?userId={sut.Administrator}");
            var sources = JellyfinApi.Field(playbackInfo, "MediaSources")!.AsArray();
            var advertisedPath = JellyfinApi.Value<string>(sources[0], "Path");

            // Scanner probes stay short; sustained playback allows a real peer scheduling pulse.
            await sut.ConfigureAsync(waitSeconds: 30);
            var playback = await ProgressivePlayback.CaptureAsync(sut, client, hostUrl, cancellationToken);
            var history = await ViewerHistory.SaveAsync(sut, projectedItemId, states);
            await sut.ImportAsync();
            await sut.AddLibraryAsync("Imported movies", sut.ImportedMoviesPath);
            var importedBytes = await client.GetByteArrayAsync(hostUrl, cancellationToken);
            var seedingItem = await sut.FindItemAsync(true);
            var seedingStatus = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            var torrentStillPresent = await sut.Swarm.ProgressAsync(cancellationToken);
            await history.ShouldBeRetainedAsync(sut.Api);

            // Torrent removal is the lifecycle boundary; import alone must keep the projected item.
            await sut.Swarm.RemoveAsync(cancellationToken);
            var completion = await sut.WaitForCompletionAsync(projectionId);
            var nativeItemId = JellyfinApi.Value<string>(completion, "NativeItemId");
            var nativeItem = await sut.FindItemAsync(false);
            var afterHandoff = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            using var retiredStream = await client.GetAsync(hostUrl, cancellationToken);
            await history.ShouldBeRetainedAsync(sut.Api, nativeItemId);
            var nativeBytes = await sut.Api.ReadVideoAsync(nativeItemId, cancellationToken);

            // The native file must remain playable with download scanning disabled, including after restart.
            await sut.ConfigureAsync(enabled: false);
            await sut.RestartAsync();
            var restartedItemId = await sut.WaitForItemAsync(false);
            var restartedBytes = await sut.Api.ReadVideoAsync(restartedItemId, cancellationToken);
            var restartedDetails = await sut.Api.RequestAsync($"/Users/{sut.Administrator}/Items/{restartedItemId}");
            var restartedStatus = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            using var retiredAfterRestart = await client.GetAsync(sut.HostStream(permanentUrl), cancellationToken);

            // Assert
            indexingDownload.Complete.Should().BeFalse("Jellyfin must advertise the projected item before the download finishes");
            (advertisedPath == permanentUrl).Should().BeTrue("Jellyfin playback must resolve the projected stream link");
            playback.ShouldMatch(sut.ExpectedBytes, sut.Swarm.PieceSize);
            importedBytes.Should().Equal(sut.ExpectedBytes);
            torrentStillPresent.Complete.Should().BeTrue();
            JellyfinApi.Value<string>(seedingItem, "Id").Should().Be(projectedItemId);
            JellyfinApi.Field(seedingStatus, "Entries")!.AsArray().Should().ContainSingle();
            JellyfinApi.Field(seedingStatus, "Completed")!.AsArray().Should().BeEmpty();
            nativeItemId.Should().NotBe(projectedItemId);
            JellyfinApi.Value<string>(nativeItem, "Id").Should().Be(nativeItemId);
            restartedItemId.Should().Be(nativeItemId);
            nativeBytes.Should().Equal(sut.ExpectedBytes);
            restartedBytes.Should().Equal(sut.ExpectedBytes);
            retiredStream.StatusCode.Should().Be(HttpStatusCode.NotFound);
            retiredAfterRestart.StatusCode.Should().Be(HttpStatusCode.NotFound);
            JellyfinApi.Field(afterHandoff, "Entries")!.AsArray().Should().BeEmpty();
            JellyfinApi.Value<bool>(restartedStatus, "Enabled").Should().BeFalse();
            JellyfinApi.Field(restartedStatus, "Entries")!.AsArray().Should().BeEmpty();
            var persistedCompletion = JellyfinApi.Field(restartedStatus, "Completed")!.AsArray().Should().ContainSingle().Subject;
            JsonNode.DeepEquals(persistedCompletion, completion).Should().BeTrue("completion history survives a disabled-plugin restart");
            JellyfinApi.Value<string>(completion, "Title").Should().Be(sut.Swarm.Title);
            JellyfinApi.Value<DateTimeOffset>(completion, "CompletedAt").Should().NotBe(default);
            File.Exists(generatedStream).Should().BeFalse();
            File.Exists(generatedMetadata).Should().BeFalse();
            File.Exists(generatedStream + ".new").Should().BeFalse();
            File.Exists(generatedMetadata + ".new").Should().BeFalse();
            File.Exists(importedFile).Should().BeTrue();
            (await sut.FindItemAsync(true)).Should().BeNull();
            downloadingOverview.Should().Contain(activityAddress);
            var overview = JellyfinApi.Value<string>(restartedDetails, "Overview");
            overview.Should().Contain(activityAddress);
            overview.Split("View download status", StringSplitOptions.None).Should().HaveCount(2, "repeated polls and handoff must keep one activity link");
            await history.ShouldBeRetainedAsync(sut.Api, restartedItemId);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private async Task<string?> WaitForActivityLinkAsync(string itemId, string activityAddress)
    {
        string? overview = null;
        await JellyfinTestServer.UntilAsync(async () =>
        {
            var details = await sut.Api.RequestAsync($"/Users/{sut.Administrator}/Items/{itemId}");
            overview = JellyfinApi.Field(details, "Overview")?.GetValue<string>();
            return overview?.Contains(activityAddress, StringComparison.Ordinal) == true;
        }, "download activity link in the movie description");
        return overview;
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture();
        fixture.Customize(new AutoMoqCustomization());
        fixture.Customize<WatchState>(composer => composer.With(value => value.Rating, () => fixture.Create<int>() % 11d));
        return fixture;
    });
}

public sealed record WatchState
{
    public bool Played { get; set; }
    public long PlaybackPositionTicks { get; set; }
    public int PlayCount { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime? LastPlayedDate { get; set; }
    public double? Rating { get; set; }
    public bool? Likes { get; set; }
}
