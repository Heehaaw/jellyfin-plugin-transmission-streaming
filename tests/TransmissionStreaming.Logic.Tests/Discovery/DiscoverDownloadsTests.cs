namespace TransmissionStreaming.Logic.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Moq;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Discovery;
using TransmissionStreaming.Logic.Imports;
using Xunit;

public sealed class DiscoverDownloadsTests
{
    [Theory, AutoMoqData]
    internal async Task Should_CreateOnlyThePrimaryMovie(DiscoveryScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IArrClient> arr, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionPaths> paths, [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IMediator> mediator, DiscoverDownloads.Handler sut)
    {
        // Arrange
        var extra = source.Torrent.Files[0];
        var feature = source.Torrent.Files[1];
        extra.Name += ".mkv";
        feature.Name += ".mkv";
        feature.Length += extra.Length;
        source.Identity.Kind = MediaKind.Movie;
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true });
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([source.Torrent]);
        arr.Setup(value => value.FindDownloadAsync(It.IsAny<ArrConnection>(), MediaKind.Movie,
            source.Torrent.Hash, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(source.Identity);
        store.Setup(value => value.All()).Returns([]);
        paths.Setup(value => value.Id(source.Identity)).Returns(source.Existing.Id);
        paths.Setup(value => value.StreamPath(It.IsAny<string>(), source.Identity)).Returns(source.Existing.StreamPath);
        mediator.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));
        library.Setup(value => value.RefreshAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        library.Setup(value => value.ReconcileAsync(It.IsAny<Projection>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var saved = new List<Projection>();
        store.Setup(value => value.Save(It.IsAny<Projection>())).Callback<Projection>(saved.Add);

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        saved.Should().NotBeEmpty().And.OnlyContain(entry => entry.SourceName == feature.Name && entry.Length == feature.Length);
        files.Verify(value => value.EnsureReady(), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepIdentityAcrossCompletion(DiscoveryScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator, DiscoverDownloads.Handler sut)
    {
        // Arrange
        source.Existing.CompletedPath = null;
        var id = source.Existing.Id;
        var secret = source.Existing.Secret;
        var streamPath = source.Existing.StreamPath;
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true });
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(value => value.All()).Returns([source.Existing]);
        mediator.Setup(value => value.Send(It.Is<IQuery<string?>>(query => query is ResolveImportedFile.Query),
            It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(source.ImportedPath));
        library.Setup(value => value.ReconcileAsync(source.Existing, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        source.Existing.CompletedPath.Should().Be(source.ImportedPath);
        source.Existing.Progress.Should().Be(1d);
        source.Existing.Id.Should().Be(id);
        source.Existing.Secret.Should().Be(secret);
        source.Existing.StreamPath.Should().Be(streamPath);
        library.Verify(value => value.ReconcileAsync(source.Existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotDiscoverWhenDisabled(DiscoveryScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionFiles> files, DiscoverDownloads.Handler sut)
    {
        // Arrange
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = false });

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>()), Times.Never);
        files.Verify(value => value.EnsureReady(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReportUnavailableDownloadWithoutLosingItsIdentity(DiscoveryScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<IMediator> mediator, DiscoverDownloads.Handler sut)
    {
        // Arrange
        source.Existing.CompletedPath = null;
        var previousProgress = source.Existing.Progress;
        const string unavailable = "Download unavailable";
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true });
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(value => value.All()).Returns([source.Existing]);
        mediator.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        source.Existing.Status.Should().Be(unavailable);
        source.Existing.Progress.Should().Be(previousProgress);
        store.Verify(value => value.Save(source.Existing), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ExposeDownloadErrorsForAnExistingStream(DiscoveryScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store, [Frozen] Mock<IMediator> mediator, DiscoverDownloads.Handler sut)
    {
        // Arrange
        source.Existing.CompletedPath = null;
        source.Existing.TorrentHash = source.Torrent.Hash;
        source.Existing.FileIndex = 0;
        source.Existing.Length = source.Torrent.Files[0].Length;
        const string failed = "Download error";
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true });
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([source.Torrent]);
        store.Setup(value => value.All()).Returns([source.Existing]);
        mediator.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        source.Existing.Status.Should().Be(failed);
        source.Existing.Progress.Should().Be(Math.Clamp((double)source.Torrent.Files[0].BytesCompleted / source.Existing.Length, 0, 1));
    }

    [Theory, AutoMoqData]
    internal async Task Should_IgnoreDownloadsWithoutArrOwnership(DiscoveryScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IArrClient> arr, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionFiles> files, DiscoverDownloads.Handler sut)
    {
        // Arrange
        foreach (var file in source.Torrent.Files)
        {
            file.Name += ".mkv";
        }
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true });
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([source.Torrent]);
        store.Setup(value => value.All()).Returns([]);
        arr.Setup(value => value.FindDownloadAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaKind>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaIdentity?)null);

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        store.Verify(value => value.Save(It.IsAny<Projection>()), Times.Never);
        files.Verify(value => value.WriteFiles(It.IsAny<Projection>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 2 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Register<IVideoFileSelection>(() => fixture.Create<VideoFileSelection>());
        return fixture;
    });
}

public sealed record DiscoveryScenario(DiscoverDownloads.Command Command, StreamingOptions Options,
    TorrentSnapshot Torrent, MediaIdentity Identity, Projection Existing, string ImportedPath);
