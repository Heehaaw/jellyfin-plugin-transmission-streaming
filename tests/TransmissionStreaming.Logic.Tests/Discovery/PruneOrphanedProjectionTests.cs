namespace TransmissionStreaming.Logic.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Logic.Discovery;
using Xunit;

public sealed class PruneOrphanedProjectionTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PreserveAnExistingTorrent(
        PruneOrphanedProjection.Command source,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionFiles> files,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        var command = source with
        {
            TorrentPresent = true
        };

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeFalse();
        arr.Verify(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
        files.Verify(value => value.RemoveFiles(It.IsAny<Projection>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveAnActiveReader(
        PruneOrphanedProjection.Command source,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IArrClient> arr,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(1);

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeFalse();
        arr.Verify(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveAnExistingImportedFile(
        PruneOrphanedProjection.Command source,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IMediaFiles> media,
        [Frozen] Mock<IArrClient> arr,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        media.Setup(value => value.Exists(source.Entry.CompletedPath!)).Returns(true);

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeFalse();
        arr.Verify(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveAnEntryWithoutAConfiguredOwner(
        PruneOrphanedProjection.Command source,
        StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IArrClient> arr,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.CompletedPath = null;
        source.Entry.Identity.Kind = MediaKind.Movie;
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                RadarrUrl = ""
            }
        );

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeFalse();
        arr.Verify(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepAnArrOwnedEntryForLateImport(
        PruneOrphanedProjection.Command source,
        StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionLibrary> library,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.CompletedPath = null;
        source.Entry.Identity.Kind = MediaKind.Movie;
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        options.SetupGet(value => value.CurrentValue).Returns(configuration);
        arr.Setup(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), source.Entry.Identity, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeFalse();
        library.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveStateUntouchedWhenArrIsUnavailable(
        PruneOrphanedProjection.Command source,
        StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IProjectionStore> store,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.CompletedPath = null;
        source.Entry.Identity.Kind = MediaKind.Movie;
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        options.SetupGet(value => value.CurrentValue).Returns(configuration);
        arr.Setup(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), source.Entry.Identity, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());

        // Act
        var action = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        files.Verify(value => value.RemoveFiles(It.IsAny<Projection>()), Times.Never);
        store.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_DeferCleanupWhileTheHostItemIsPlaying(
        PruneOrphanedProjection.Command source,
        StreamingOptions configuration,
        LibraryItem projected,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IProjectionFiles> files,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.CompletedPath = null;
        source.Entry.Identity.Kind = MediaKind.Movie;
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        options.SetupGet(value => value.CurrentValue).Returns(configuration);
        arr.Setup(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), source.Entry.Identity, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(
            projected with
            {
                IsPlaying = true
            }
        );

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeFalse();
        files.Verify(value => value.RemoveFiles(It.IsAny<Projection>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RemoveAnOrphanInRetryableOrderUsingItsOwner(
        PruneOrphanedProjection.Command source,
        StreamingOptions configuration,
        LibraryItem projected,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IMediaFiles> media,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IProjectionStore> store,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.Identity.Kind = MediaKind.Episode;
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        media.Setup(value => value.Exists(source.Entry.CompletedPath!)).Returns(false);
        options.SetupGet(value => value.CurrentValue).Returns(configuration);
        arr.Setup(value => value.IsMediaTrackedAsync(
                    connection: It.Is<ArrConnection>(connection =>
                        connection.Url == configuration.SonarrUrl && connection.ApiKey == configuration.SonarrApiKey
                    ),
                    identity: source.Entry.Identity,
                    cancellationToken: It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        var operations = new List<string>();
        var hostStep = "host";
        var filesStep = "files";
        var storeStep = "store";
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(
            projected with
            {
                IsPlaying = false
            }
        );
        library.Setup(value => value.Remove(projected.Id)).Callback(() => operations.Add(hostStep));
        files.Setup(value => value.RemoveFiles(source.Entry)).Callback(() => operations.Add(filesStep));
        store.Setup(value => value.Remove(source.Entry.Id)).Callback(() => operations.Add(storeStep));

        // Act
        var removed = await sut.Handle(command, CancellationToken.None);

        // Assert
        removed.Should().BeTrue();
        operations.Should().Equal(hostStep, filesStep, storeStep);
        arr.Verify(
            expression: value => value.IsMediaTrackedAsync(
                connection: It.Is<ArrConnection>(connection =>
                    connection.Url == configuration.SonarrUrl && connection.ApiKey == configuration.SonarrApiKey
                ),
                identity: source.Entry.Identity,
                cancellationToken: It.IsAny<CancellationToken>()
            ),
            times: Times.Once
        );
    }

    [Theory, AutoMoqData]
    internal async Task Should_RetainTheRecordWhenFileCleanupFails(
        PruneOrphanedProjection.Command source,
        StreamingOptions configuration,
        LibraryItem projected,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IStreamActivity> activity,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IProjectionFiles> files,
        [Frozen] Mock<IProjectionStore> store,
        PruneOrphanedProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.CompletedPath = null;
        source.Entry.Identity.Kind = MediaKind.Movie;
        var command = source with
        {
            TorrentPresent = false
        };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        options.SetupGet(value => value.CurrentValue).Returns(configuration);
        arr.Setup(value => value.IsMediaTrackedAsync(It.IsAny<ArrConnection>(), source.Entry.Identity, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(
            projected with
            {
                IsPlaying = false
            }
        );
        files.Setup(value => value.RemoveFiles(source.Entry)).Throws<IOException>();

        // Act
        var action = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
        store.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            return fixture;
        }
    );
}
