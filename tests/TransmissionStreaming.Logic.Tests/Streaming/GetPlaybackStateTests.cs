namespace TransmissionStreaming.Logic.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Configuration;
using TransmissionStreaming.Logic.Streaming;
using Xunit;

public sealed class GetPlaybackStateTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ThrottleStoreAndRpcRefreshesAtTheMonotonicBoundary(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<TimeProvider> clock, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        var timestamp = 0L;
        var interval = TimeSpan.FromMilliseconds(500);
        clock.Setup(value => value.GetTimestamp()).Returns(() => timestamp);

        // Act
        var initial = await sut.Handle(source.Query, CancellationToken.None);
        for (var index = 0; index < 10; index++) await sut.Handle(source.Query, CancellationToken.None);
        timestamp = interval.Ticks - 1;
        await sut.Handle(source.Query, CancellationToken.None);
        var callsBeforeBoundary = store.Invocations.Count;
        clock.Setup(value => value.GetUtcNow()).Returns(DateTimeOffset.MaxValue);
        timestamp = interval.Ticks;
        await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        initial.Length.Should().Be(source.Query.Playback.Entry.Length);
        callsBeforeBoundary.Should().Be(1);
        store.Verify(value => value.Get(source.Query.Playback.Entry.Id), Times.Exactly(2));
        client.Verify(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), source.Query.Playback.Entry.TorrentHash, It.IsAny<CancellationToken>()), Times.Exactly(2));
        client.Verify(value => value.GetSessionAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        clock.Verify(value => value.GetUtcNow(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_CacheAnAbsentTorrentUntilTheNextRefresh(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> client, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        client.Setup(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), source.Query.Playback.Entry.TorrentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TorrentSnapshot?)null);

        // Act
        var initial = await sut.Handle(source.Query, CancellationToken.None);
        await sut.Handle(source.Query, CancellationToken.None);
        await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        initial.Path.Should().BeNull();
        initial.Length.Should().Be(source.Query.Playback.Entry.Length);
        client.Verify(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), source.Query.Playback.Entry.TorrentHash, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(value => value.Get(source.Query.Playback.Entry.Id), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_MapAndFindRenamedIncompleteFiles(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<ITransmissionClient> client,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaFiles> files, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        source.Torrent.DownloadDirectory = "/remote/completed";
        source.Torrent.SequentialDownload = true;
        source.Session.IncompleteDirectoryEnabled = true;
        source.Session.IncompleteDirectory = "/remote/incomplete";
        source.Session.RenamePartialFiles = true;
        var remote = source.Session.IncompleteDirectory + "/" + source.Torrent.Files[0].Name + ".part";
        var local = Path.Combine(source.Configuration.LocalMediaRoot, source.Torrent.Files[0].Name + ".part");
        mediator.Setup(value => value.Send(It.Is<MapServicePath.Query>(query => query.Service == MediaService.Transmission && query.Path == remote), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string?>(local));
        files.Setup(value => value.Exists(local)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Path.Should().Be(local);
        result.DiskWritesSafe.Should().BeTrue();
        result.Complete.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_PlayAnImportedEpisodeWithoutContactingTransmission(SourceScenario source,
        [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<ITransmissionClient> client,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaFiles> files, GetPlaybackState.Handler sut)
    {
        // Arrange
        var entry = source.Query.Playback.Entry;
        entry.Identity.Kind = MediaKind.Episode;
        entry.CompletedPath = Path.Combine(source.Configuration.LocalMediaRoot, source.Torrent.Files[0].Name);
        store.Setup(value => value.Get(entry.Id)).Returns(entry);
        mediator.Setup(value => value.Send(It.Is<ValidateLocalMediaPath.Query>(query => query.Service == MediaService.Sonarr && query.Path == entry.CompletedPath),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string?>(entry.CompletedPath));
        files.Setup(value => value.HasLength(entry.CompletedPath, entry.Length)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Path.Should().Be(entry.CompletedPath);
        result.Complete.Should().BeTrue();
        client.Verify(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_FollowAnImportAfterPlaybackHasStarted(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<ITransmissionClient> client,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaFiles> files, [Frozen] Mock<TimeProvider> clock, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        var entry = source.Query.Playback.Entry;
        var imported = Path.Combine(source.Configuration.LocalMediaRoot, source.Torrent.Files[0].Name);
        mediator.Setup(value =>
                value.Send(It.Is<ValidateLocalMediaPath.Query>(query => query.Service == MediaService.Radarr && query.Path == imported), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string?>(imported));
        files.Setup(value => value.HasLength(imported, entry.Length)).Returns(true);
        await sut.Handle(source.Query, CancellationToken.None);
        entry.CompletedPath = imported;
        clock.Setup(value => value.GetTimestamp()).Returns(TimeSpan.FromMilliseconds(500).Ticks);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Path.Should().Be(imported);
        result.Complete.Should().BeTrue();
        client.Verify(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), entry.TorrentHash, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RetainVerifiedMetadataWhenTheTorrentIsRemoved(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<TimeProvider> clock, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        client.SetupSequence(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), source.Query.Playback.Entry.TorrentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source.Torrent).ReturnsAsync((TorrentSnapshot?)null);
        await sut.Handle(source.Query, CancellationToken.None);
        clock.Setup(value => value.GetTimestamp()).Returns(TimeSpan.FromMilliseconds(500).Ticks);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Pieces.Should().Equal(Convert.FromBase64String(source.Torrent.Pieces));
        result.PieceSize.Should().Be(source.Torrent.PieceSize);
        source.Query.Playback.Torrent.Should().BeSameAs(source.Torrent);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefreshEachPlaybackIndependently(SourceScenario source, GetPlaybackState.Query other,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> client, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        other = other with { Playback = other.Playback with { Entry = source.Query.Playback.Entry } };

        // Act
        await sut.Handle(source.Query, CancellationToken.None);
        await sut.Handle(other, CancellationToken.None);

        // Assert
        other.Playback.Should().NotBeSameAs(source.Query.Playback);
        client.Verify(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), source.Query.Playback.Entry.TorrentHash, It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(value => value.Get(source.Query.Playback.Entry.Id), Times.Exactly(2));
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseAChangedReleaseLength(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> client, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        source.Torrent.Files[0].Length++;

        // Act
        var action = async () => await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseMalformedPieceMaps(SourceScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> client, GetPlaybackState.Handler sut)
    {
        // Arrange
        source.SetUp(options, store, client);
        source.Torrent.Pieces = "!";

        // Act
        var action = async () => await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }

    internal sealed record SourceScenario(GetPlaybackState.Query Query, TorrentSnapshot Torrent, TransmissionSessionSnapshot Session, StreamingOptions Configuration)
    {
        public void SetUp(Mock<IOptionsMonitor<StreamingOptions>> options, Mock<IProjectionStore> store, Mock<ITransmissionClient> client)
        {
            var entry = Query.Playback.Entry;
            Torrent.Files[entry.FileIndex].Length = entry.Length;
            options.SetupGet(value => value.CurrentValue).Returns(Configuration);
            store.Setup(value => value.Get(entry.Id)).Returns(entry);
            client.Setup(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), entry.TorrentHash, It.IsAny<CancellationToken>())).ReturnsAsync(Torrent);
            client.Setup(value => value.GetSessionAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync(Session);
        }
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<Projection>(composer => composer.With(value => value.FileIndex, 0).With(value => value.CompletedPath, (string?)null));
        fixture.Customize<MediaIdentity>(composer => composer.With(value => value.Kind, MediaKind.Movie));
        fixture.Customize<PlaybackSession>(composer => composer.OmitAutoProperties());
        fixture.Customize<TorrentSnapshot>(composer => composer.With(value => value.Pieces, Convert.ToBase64String(fixture.Create<byte[]>())));
        fixture.Customize<Mock<TimeProvider>>(composer => composer.Do(clock =>
        {
            clock.SetupGet(value => value.TimestampFrequency).Returns(TimeSpan.TicksPerSecond);
            clock.Setup(value => value.GetTimestamp()).Returns(0);
        }));
        fixture.Register<TimeProvider>(() => fixture.Create<Mock<TimeProvider>>().Object);
        fixture.Customize<Mock<IMediaFiles>>(composer => composer.Do(files => files.Setup(value => value.Exists(It.IsAny<string>())).Returns(false)));
        return fixture;
    });
}
