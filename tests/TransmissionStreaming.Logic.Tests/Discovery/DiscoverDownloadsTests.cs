namespace TransmissionStreaming.Logic.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Discovery;
using TransmissionStreaming.Logic.Imports;
using TransmissionStreaming.Logic.Library;
using Xunit;

public sealed class DiscoverDownloadsTests
{
    [Theory, AutoMoqData]
    internal async Task Should_CreateOnlyThePrimaryMovie(
        DiscoveryScenario source,
        SelectVideoFiles.Result selection,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionPaths> paths,
        [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        var extra = source.Torrent.Files[0];
        var feature = source.Torrent.Files[1];
        extra.Name += ".mkv";
        feature.Name += ".mkv";
        feature.Length += extra.Length;
        source.Identity.Kind = MediaKind.Movie;
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([source.Torrent]);
        arr.Setup(value => value.FindDownloadAsync(
                connection: It.IsAny<ArrConnection>(),
                kind: MediaKind.Movie,
                hash: source.Torrent.Hash,
                filename: It.IsAny<string>(),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(source.Identity);
        store.Setup(value => value.All()).Returns([]);
        paths.Setup(value => value.Id(source.Identity)).Returns(source.Existing.Id);
        paths.Setup(value => value.StreamPath(It.IsAny<string>(), source.Identity)).Returns(source.Existing.StreamPath);
        mediator.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));
        library.Setup(value => value.RefreshAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var selected = selection with
        {
            PlayableIndexes = Enumerable.Range(0, source.Torrent.Files.Count).ToArray(),
            PrimaryMovieIndex = source.Torrent.Files.IndexOf(feature)
        };
        mediator.Setup(value => value.Send(It.Is<SelectVideoFiles.Query>(query => query.Files == source.Torrent.Files), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(selected));
        var saved = new List<Projection>();
        store.Setup(value => value.Save(It.IsAny<Projection>())).Callback<Projection>(saved.Add);

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        saved.Should().NotBeEmpty().And.OnlyContain(entry => entry.SourceName == feature.Name && entry.Length == feature.Length);
        files.Verify(value => value.EnsureReady(), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepIdentityAcrossCompletion(
        DiscoveryScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        source.Existing.CompletedPath = null;
        var id = source.Existing.Id;
        var secret = source.Existing.Secret;
        var streamPath = source.Existing.StreamPath;
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(value => value.All()).Returns([source.Existing]);
        mediator.Setup(value => value.Send(
                query: It.Is<IQuery<string?>>(query => query is ResolveImportedFile.Query),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).Returns(ValueTask.FromResult<string?>(source.ImportedPath));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        source.Existing.CompletedPath.Should().Be(source.ImportedPath);
        source.Existing.Progress.Should().Be(1d);
        source.Existing.Id.Should().Be(id);
        source.Existing.Secret.Should().Be(secret);
        source.Existing.StreamPath.Should().Be(streamPath);
        mediator.Verify(value => value.Send(It.Is<ReconcileProjection.Command>(request => request.Entry == source.Existing), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotDiscoverWhenDisabled(
        DiscoveryScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionFiles> files,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = false
            }
        );

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>()), Times.Never);
        files.Verify(value => value.EnsureReady(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReportUnavailableDownloadWithoutLosingItsIdentity(
        DiscoveryScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IMediator> mediator,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        source.Existing.CompletedPath = null;
        var previousProgress = source.Existing.Progress;
        const string unavailable = "Download unavailable";
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
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
    internal async Task Should_ExposeDownloadErrorsForAnExistingStream(
        DiscoveryScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IMediator> mediator,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        source.Existing.CompletedPath = null;
        source.Existing.TorrentHash = source.Torrent.Hash;
        source.Existing.FileIndex = 0;
        source.Existing.Length = source.Torrent.Files[0].Length;
        const string failed = "Download error";
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
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
    internal async Task Should_IgnoreDownloadsWithoutArrOwnership(
        DiscoveryScenario source,
        SelectVideoFiles.Result selection,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IMediator> mediator,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        foreach (var file in source.Torrent.Files)
        {
            file.Name += ".mkv";
        }

        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([source.Torrent]);
        store.Setup(value => value.All()).Returns([]);
        arr.Setup(value => value.FindDownloadAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaKind>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaIdentity?)null);

        mediator.Setup(value => value.Send(It.IsAny<SelectVideoFiles.Query>(), It.IsAny<CancellationToken>()))
            .Returns(
                ValueTask.FromResult(
                    selection with
                    {
                        PlayableIndexes = Enumerable.Range(0, source.Torrent.Files.Count).ToArray()
                    }
                )
            );

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        store.Verify(value => value.Save(It.IsAny<Projection>()), Times.Never);
        files.Verify(value => value.WriteFiles(It.IsAny<Projection>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotRewriteOrReconcileAPrunedProjection(
        DiscoveryScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        DiscoverDownloads.Handler sut
    )
    {
        // Arrange
        source.Existing.CompletedPath = null;
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(value => value.All()).Returns([source.Existing]);
        mediator.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));
        mediator.Setup(value => value.Send(
                command: It.Is<PruneOrphanedProjection.Command>(command =>
                    command.Entry == source.Existing && !command.TorrentPresent
                ),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).Returns(ValueTask.FromResult(true));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        store.Verify(value => value.Save(It.IsAny<Projection>()), Times.Never);
        files.Verify(value => value.WriteFiles(It.IsAny<Projection>()), Times.Never);
        mediator.Verify(value => value.Send(It.IsAny<ReconcileProjection.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 2
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            fixture.Customize<SelectVideoFiles.Result>(composer =>
                composer.With(value => value.PlayableIndexes, Array.Empty<int>()).With(value => value.PrimaryMovieIndex, (int?)null)
            );
            fixture.Customize<Mock<IMediator>>(composer => composer.Do(mock => mock
                    .Setup(value => value.Send(It.IsAny<SelectVideoFiles.Query>(), It.IsAny<CancellationToken>()))
                    .Returns(ValueTask.FromResult(fixture.Create<SelectVideoFiles.Result>()))
                )
            );
            return fixture;
        }
    );
}

public sealed record DiscoveryScenario(
    DiscoverDownloads.Command Command,
    StreamingOptions Options,
    TorrentSnapshot Torrent,
    MediaIdentity Identity,
    Projection Existing,
    string ImportedPath
);
