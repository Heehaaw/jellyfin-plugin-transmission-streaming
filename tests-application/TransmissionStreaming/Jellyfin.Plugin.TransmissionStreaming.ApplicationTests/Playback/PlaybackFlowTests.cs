namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
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
        PrepareViewerStates(playedViewer, resumingViewer);
        var states = new[]
        {
            playedViewer, resumingViewer
        };
        var cancellationToken = TestContext.Current.CancellationToken;
        var permanentUrl = await sut.WaitForStreamAsync();
        var projectionRoot = Path.Combine(sut.Workspace, "projection");
        var generatedStream = Directory.EnumerateFiles(projectionRoot, "*.strm", SearchOption.AllDirectories).Single();
        var activityAddress = new Uri(JellyfinContainer.InternalAddress, "web/#/configurationpage?name=transmission-streaming").AbsoluteUri;

        try
        {
            // Act
            var download = await CaptureDownloadAsync(permanentUrl, activityAddress, cancellationToken);
            var history = await ViewerHistory.SaveAsync(sut, download.ItemId, states);
            var seeding = await CaptureSeedingAsync(permanentUrl, cancellationToken);
            await history.ShouldBeRetainedAsync(sut.Api);

            var handoff = await CaptureHandoffAsync(permanentUrl, cancellationToken);
            await history.ShouldBeRetainedAsync(sut.Api, handoff.ItemId);

            var restarted = await CaptureRestartAsync(permanentUrl, cancellationToken);

            // Assert
            AssertDownload(download, activityAddress);
            AssertSeeding(seeding, download.ItemId);
            AssertHandoff(handoff, download.ItemId);
            AssertRestart(restarted, handoff, activityAddress);
            AssertGeneratedFilesRemoved(generatedStream);
            await history.ShouldBeRetainedAsync(sut.Api, restarted.ItemId);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private static void PrepareViewerStates(WatchState playedViewer, WatchState resumingViewer)
    {
        playedViewer.Played = true;
        playedViewer.IsFavorite = true;
        playedViewer.PlaybackPositionTicks = 0;

        resumingViewer.Played = false;
        resumingViewer.IsFavorite = false;
        var maximumResumeSeconds = (long)(JellyfinContainer.MediaDuration.TotalSeconds / 2);
        var resumeSeconds = resumingViewer.PlaybackPositionTicks % maximumResumeSeconds + 1;
        resumingViewer.PlaybackPositionTicks = resumeSeconds * TimeSpan.TicksPerSecond;
    }

    private async Task<DownloadPlayback> CaptureDownloadAsync(string permanentUrl, string activityAddress, CancellationToken cancellationToken)
    {
        await sut.AddLibraryAsync("Streaming", sut.ProjectionMoviesPath, sut.ImportedMoviesPath);
        // Settle the initial scan while the import root is empty. Handoff must later index the physical folder itself.
        await sut.WaitForLibraryScanIdleAsync();
        var folderResponse = await sut.Api.RequestAsync("/Library/VirtualFolders");
        var folders = folderResponse!.AsArray();
        var library = folders.Single();
        var libraryType = JellyfinApi.Value<string>(library, "CollectionType");
        var locations = JellyfinApi.Field(library, "Locations")!.AsArray();
        var libraryPaths = locations.Select(path => path!.GetValue<string>()).ToArray();

        var itemId = await sut.WaitForItemAsync();
        var overview = await WaitForActivityLinkAsync(itemId, activityAddress);
        var download = await sut.Swarm.ProgressAsync(cancellationToken);
        var playbackInfo = await sut.Api.RequestAsync($"/Items/{itemId}/PlaybackInfo?userId={sut.Administrator}");
        var sources = JellyfinApi.Field(playbackInfo, "MediaSources")!.AsArray();
        var advertisedPath = JellyfinApi.Value<string>(sources[0], "Path");

        // Scanner probes stay short; sustained playback allows a real peer scheduling pulse.
        await sut.ConfigureAsync(waitSeconds: 30);
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        var hostUrl = sut.HostStream(permanentUrl);
        var playback = await ProgressivePlayback.CaptureAsync(sut, client, hostUrl, cancellationToken);
        return new DownloadPlayback(itemId, overview, download.Complete, advertisedPath == permanentUrl, playback, libraryType, libraryPaths);
    }

    private async Task<string?> WaitForActivityLinkAsync(string itemId, string activityAddress)
    {
        string? overview = null;
        await JellyfinTestServer.UntilAsync(
            probe: async () =>
            {
                var details = await sut.Api.RequestAsync($"/Users/{sut.Administrator}/Items/{itemId}");
                overview = JellyfinApi.Field(details, "Overview")?.GetValue<string>();
                return overview?.Contains(activityAddress, StringComparison.Ordinal) == true;
            },
            description: "download activity link in the movie description"
        );
        return overview;
    }

    private async Task<SeedingPlayback> CaptureSeedingAsync(string permanentUrl, CancellationToken cancellationToken)
    {
        await sut.ImportAsync();
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        var hostUrl = sut.HostStream(permanentUrl);
        var bytes = await client.GetByteArrayAsync(hostUrl, cancellationToken);
        var item = await sut.FindItemAsync(true);
        var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
        var entries = JellyfinApi.Field(status, "Entries")!.AsArray();
        var completed = JellyfinApi.Field(status, "Completed")!.AsArray();
        var torrent = await sut.Swarm.ProgressAsync(cancellationToken);
        var itemId = JellyfinApi.Value<string>(item, "Id");
        return new SeedingPlayback(bytes, itemId, torrent.Complete, entries.Count, completed.Count);
    }

    private async Task<NativePlayback> CaptureHandoffAsync(string permanentUrl, CancellationToken cancellationToken)
    {
        // Torrent removal is the lifecycle boundary; import alone must keep the projected item.
        await sut.Swarm.RemoveAsync(cancellationToken);
        var projectionId = new Uri(permanentUrl).Segments.Last();
        var completion = await sut.WaitForCompletionAsync(projectionId);
        var itemId = JellyfinApi.Value<string>(completion, "NativeItemId");
        var item = await sut.FindItemAsync(false);
        var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
        var entries = JellyfinApi.Field(status, "Entries")!.AsArray();
        var proxyStatus = await ReadProxyStatusAsync(permanentUrl, cancellationToken);
        var bytes = await sut.Api.ReadVideoAsync(itemId, cancellationToken);
        var indexedItemId = JellyfinApi.Value<string>(item, "Id");
        return new NativePlayback(completion, itemId, indexedItemId, bytes, proxyStatus, entries.Count);
    }

    private async Task<HttpStatusCode> ReadProxyStatusAsync(string permanentUrl, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        var hostUrl = sut.HostStream(permanentUrl);
        using var response = await client.GetAsync(hostUrl, cancellationToken);
        return response.StatusCode;
    }

    private async Task<RestartedPlayback> CaptureRestartAsync(string permanentUrl, CancellationToken cancellationToken)
    {
        // Native playback must work with download scanning disabled, including after restart.
        await sut.ConfigureAsync(false);
        await sut.RestartAsync();
        var itemId = await sut.WaitForItemAsync(false);
        var bytes = await sut.Api.ReadVideoAsync(itemId, cancellationToken);
        var details = await sut.Api.RequestAsync($"/Users/{sut.Administrator}/Items/{itemId}");
        var overview = JellyfinApi.Value<string>(details, "Overview");
        var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
        var entries = JellyfinApi.Field(status, "Entries")!.AsArray();
        var completed = JellyfinApi.Field(status, "Completed")!.AsArray();
        var proxyStatus = await ReadProxyStatusAsync(permanentUrl, cancellationToken);
        var projected = await sut.FindItemAsync(true);
        var enabled = JellyfinApi.Value<bool>(status, "Enabled");
        return new RestartedPlayback(itemId, bytes, overview, enabled, entries.Count, completed, proxyStatus, projected is not null);
    }

    private void AssertDownload(DownloadPlayback download, string activityAddress)
    {
        download.WasComplete.Should().BeFalse("Jellyfin must advertise the projected item before the download finishes");
        download.AdvertisedProxy.Should().BeTrue("Jellyfin playback must resolve the projected stream link");
        download.Playback.ShouldMatch(sut.ExpectedBytes, sut.Swarm.PieceSize);
        download.Overview.Should().Contain(activityAddress);

        const string expectedLibraryType = "movies";
        download.LibraryType.Should().Be(expectedLibraryType);
        download.LibraryPaths.Should().BeEquivalentTo(new[]
        {
            sut.ProjectionMoviesPath,
            sut.ImportedMoviesPath
        });
    }

    private void AssertSeeding(SeedingPlayback seeding, string projectedItemId)
    {
        seeding.Bytes.Should().Equal(sut.ExpectedBytes);
        seeding.TorrentComplete.Should().BeTrue();
        seeding.ItemId.Should().Be(projectedItemId);
        seeding.ActiveCount.Should().Be(1);
        seeding.CompletedCount.Should().Be(0);
    }

    private void AssertHandoff(NativePlayback handoff, string projectedItemId)
    {
        handoff.ItemId.Should().NotBe(projectedItemId);
        handoff.IndexedItemId.Should().Be(handoff.ItemId);
        handoff.Bytes.Should().Equal(sut.ExpectedBytes);
        handoff.ProxyStatus.Should().Be(HttpStatusCode.NotFound);
        handoff.ActiveCount.Should().Be(0);
        var title = JellyfinApi.Value<string>(handoff.Completion, "Title");
        var completedAt = JellyfinApi.Value<DateTimeOffset>(handoff.Completion, "CompletedAt");
        title.Should().Be(sut.Swarm.Title);
        completedAt.Should().NotBe(default);
    }

    private void AssertRestart(RestartedPlayback restarted, NativePlayback handoff, string activityAddress)
    {
        restarted.ItemId.Should().Be(handoff.ItemId);
        restarted.Bytes.Should().Equal(sut.ExpectedBytes);
        restarted.Enabled.Should().BeFalse();
        restarted.ActiveCount.Should().Be(0);
        restarted.ProxyStatus.Should().Be(HttpStatusCode.NotFound);
        restarted.ProjectedItemExists.Should().BeFalse();
        var completion = restarted.Completed.Should().ContainSingle().Subject;
        JsonNode.DeepEquals(completion, handoff.Completion).Should().BeTrue("completion history survives a disabled-plugin restart");
        restarted.Overview.Should().Contain(activityAddress);
        var linkOccurrences = restarted.Overview.Split("View download status", StringSplitOptions.None);
        linkOccurrences.Should().HaveCount(2, "repeated polls and handoff must keep one activity link");
    }

    private void AssertGeneratedFilesRemoved(string streamPath)
    {
        var metadata = Path.ChangeExtension(streamPath, ".nfo");
        File.Exists(streamPath).Should().BeFalse();
        File.Exists(metadata).Should().BeFalse();
        File.Exists(streamPath + ".new").Should().BeFalse();
        File.Exists(metadata + ".new").Should().BeFalse();
        var importedFile = Path.Combine(sut.Workspace, "movies", sut.Swarm.FileName);
        File.Exists(importedFile).Should().BeTrue();
    }

    private sealed record DownloadPlayback(string ItemId, string? Overview, bool WasComplete, bool AdvertisedProxy, ProgressivePlayback Playback,
        string LibraryType, string[] LibraryPaths);

    private sealed record SeedingPlayback(byte[] Bytes, string ItemId, bool TorrentComplete, int ActiveCount, int CompletedCount);

    private sealed record NativePlayback(JsonNode Completion, string ItemId, string IndexedItemId, byte[] Bytes, HttpStatusCode ProxyStatus, int ActiveCount);

    private sealed record RestartedPlayback(
        string ItemId,
        byte[] Bytes,
        string Overview,
        bool Enabled,
        int ActiveCount,
        JsonArray Completed,
        HttpStatusCode ProxyStatus,
        bool ProjectedItemExists
    );

    private sealed class AutoMoqDataAttribute() : ApplicationDataAttribute(() =>
        {
            var fixture = new Fixture();
            fixture.Customize(new AutoMoqCustomization());
            fixture.Customize<WatchState>(composer => composer.With(value => value.Rating, () => fixture.Create<int>() % 11d));
            return fixture;
        }
    );
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
