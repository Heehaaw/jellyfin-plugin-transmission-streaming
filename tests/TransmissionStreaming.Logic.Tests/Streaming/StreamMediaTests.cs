namespace TransmissionStreaming.Logic.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Streaming;
using Xunit;

public sealed class StreamMediaTests
{
    [Theory, AutoMoqData]
    internal async Task Should_DelegateCompletedPlaybackWithoutContactingTransmission(
        PlaybackScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IStreamActivity> activity,
        StreamMedia.Handler sut
    )
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true,
                PrioritizePlayback = true
            }
        );
        activity.Setup(value => value.Open(source.Command.Projection.Id)).Returns(source.Lease);
        CopyProgressiveMedia.Command? copied = null;
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), CancellationToken.None))
            .Callback((ICommand<Unit> command, CancellationToken _) => copied = command as CopyProgressiveMedia.Command)
            .Returns(ValueTask.FromResult(Unit.Value));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(value => value.EnableSequentialAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        copied.Should().NotBeNull();
        copied!.Projection.Should().BeSameAs(source.Command.Projection);
        copied.Destination.Should().BeSameAs(source.Command.Destination);
        copied.Offset.Should().Be(source.Command.Offset);
        copied.Count.Should().Be(source.Command.Count);
        Mock.Get(source.Lease).Verify(value => value.Dispose(), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_EnableSequentialForIncompletePlayback(
        PlaybackScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IStreamActivity> activity,
        StreamMedia.Handler sut
    )
    {
        // Arrange
        source.Command.Projection.CompletedPath = null;
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true,
                PrioritizePlayback = true
            }
        );
        activity.Setup(value => value.Open(source.Command.Projection.Id)).Returns(source.Lease);
        transmission.Setup(value => value.EnableSequentialAsync(It.IsAny<TransmissionConnection>(), source.Command.Projection.TorrentHash, It.IsAny<CancellationToken>())
        ).Returns(Task.CompletedTask);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), CancellationToken.None)).Returns(ValueTask.FromResult(Unit.Value));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(
            expression: value => value.EnableSequentialAsync(
                connection: It.Is<TransmissionConnection>(connection => connection.Url == source.Options.TransmissionUrl),
                hash: source.Command.Projection.TorrentHash,
                cancellationToken: It.IsAny<CancellationToken>()
            ),
            times: Times.Once
        );
        Mock.Get(source.Lease).Verify(value => value.Dispose(), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReleaseReaderWhenPlaybackFails(
        PlaybackScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IStreamActivity> activity,
        StreamMedia.Handler sut
    )
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true
            }
        );
        activity.Setup(value => value.Open(source.Command.Projection.Id)).Returns(source.Lease);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), CancellationToken.None)).Returns(ValueTask.FromException<Unit>(new IOException()));

        // Act
        var action = async () => await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
        Mock.Get(source.Lease).Verify(value => value.Dispose(), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectPlaybackAfterDisablement(
        PlaybackScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> mediator,
        StreamMedia.Handler sut
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
        var action = async () => await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        mediator.Verify(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RespectDisabledPrioritizationAndBoundTheStall(
        PlaybackScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IStreamActivity> activity,
        StreamMedia.Handler sut
    )
    {
        // Arrange
        source.Command.Projection.CompletedPath = null;
        const int maximumStallSeconds = 600;
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                Enabled = true,
                PrioritizePlayback = false,
                PieceWaitSeconds = maximumStallSeconds + source.Options.PieceWaitSeconds
            }
        );
        activity.Setup(value => value.Open(source.Command.Projection.Id)).Returns(source.Lease);
        CopyProgressiveMedia.Command? copied = null;
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), CancellationToken.None))
            .Callback((ICommand<Unit> command, CancellationToken _) => copied = command as CopyProgressiveMedia.Command)
            .Returns(ValueTask.FromResult(Unit.Value));

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(value => value.EnableSequentialAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        copied.Should().NotBeNull();
        copied!.Options.PrioritizeMissingPieces.Should().BeFalse();
        copied.Options.StallTimeout.Should().Be(TimeSpan.FromSeconds(maximumStallSeconds));
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
            fixture.Register<Stream>(() => new MemoryStream());
            return fixture;
        }
    );
}

public sealed record PlaybackScenario(StreamMedia.Command Command, StreamingOptions Options, IDisposable Lease);
