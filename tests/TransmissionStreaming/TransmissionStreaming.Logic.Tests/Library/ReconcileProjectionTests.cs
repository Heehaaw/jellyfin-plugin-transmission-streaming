namespace TransmissionStreaming.Logic.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Moq;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Library;
using Xunit;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;

public sealed class ReconcileProjectionTests
{
    [Theory, AutoMoqData]
    internal async Task Should_UpdateDownloadMetadata(
        ReconcileProjection.Command source,
        LibraryItem projected,
        StreamingOptions configuration,
        string formattedOverview,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IProjectionOverview> overviews,
        ReconcileProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.CompletedPath = null;
        source.Entry.Identity.Kind = MediaKind.Episode;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ShowDownloadStatus = true
            }
        );
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected);
        overviews.Setup(value => value.Format(projected.Overview, configuration.JellyfinPublicUrl)).Returns(formattedOverview);
        var expectedTitle = $"{source.Entry.Identity.EpisodeTitle} [{source.Entry.Status}]";

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        library.Verify(value => value.UpdateMetadataAsync(projected.Id, expectedTitle, formattedOverview, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_TransferDuplicateHistoryBeforeRemovingNativeMetadata(
        ReconcileProjection.Command source,
        LibraryItem projected,
        LibraryItem native,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        ReconcileProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.HandoffPending = false;
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected);
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(
            native with
            {
                IsPlaying = false
            }
        );
        var operations = new List<string>();
        var transfer = "history";
        var removal = "remove";
        mediator.Setup(value => value.Send(
                    command: It.Is<TransferUserHistory.Command>(request => request.SourceItemId == native.Id && request.TargetItemId == projected.Id),
                    cancellationToken: It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => operations.Add(transfer)).Returns(ValueTask.FromResult(Unit.Value));
        library.Setup(value => value.Remove(native.Id)).Callback(() => operations.Add(removal));

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        mediator.Verify(
            expression: value => value.Send(
                command: It.Is<TransferUserHistory.Command>(request => request.SourceItemId == native.Id && request.TargetItemId == projected.Id),
                cancellationToken: It.IsAny<CancellationToken>()
            ),
            times: Times.Once
        );
        library.Verify(value => value.Remove(native.Id), Times.Once);
        operations.Should().Equal(transfer, removal);
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveNativeMetadataDuringHandoff(
        ReconcileProjection.Command source,
        LibraryItem projected,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        ReconcileProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.HandoffPending = true;
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected);

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        mediator.Verify(value => value.Send(It.IsAny<TransferUserHistory.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        library.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepAPlayingNativeDuplicate(
        ReconcileProjection.Command source,
        LibraryItem projected,
        LibraryItem native,
        [Frozen] Mock<IProjectionLibrary> library,
        ReconcileProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.HandoffPending = false;
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected);
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(
            native with
            {
                IsPlaying = true
            }
        );

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        library.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveTheDuplicateWhenHistoryTransferFails(
        ReconcileProjection.Command source,
        LibraryItem projected,
        LibraryItem native,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        ReconcileProjection.Handler sut
    )
    {
        // Arrange
        source.Entry.HandoffPending = false;
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected);
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(
            native with
            {
                IsPlaying = false
            }
        );
        mediator.Setup(value => value.Send(It.IsAny<TransferUserHistory.Command>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        // Act
        var action = async () => await sut.Handle(source, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
        library.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
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
            return fixture;
        }
    );
}
