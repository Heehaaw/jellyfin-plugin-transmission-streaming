namespace TransmissionStreaming.Logic.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Filesystem;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Logic.Streaming;
using Moq;
using Mediator;
using Xunit;

public sealed class CopyProgressiveMediaTests
{
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
            fixture.Customize<ProgressiveReadOptions>(composer => composer
                .With(value => value.BufferSize, 4096)
                .With(value => value.PollInterval, TimeSpan.FromSeconds(1))
                .With(value => value.StallTimeout, TimeSpan.FromMinutes(2))
            );
            fixture.Register(() => new ObservableFakeTimeProvider());
            fixture.Register<TimeProvider>(() => fixture.Create<ObservableFakeTimeProvider>());
            return fixture;
        }
    );

    [Theory, AutoMoqData]
    internal async Task Should_WaitForVerifiedPieces(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        [Frozen] ObservableFakeTimeProvider clock,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var options = command.Options;
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            FileOffset = 0,
            PieceSize = media.Content.Length,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        mediator.SetupSequence(x => x.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state).ReturnsAsync(
                state with
                {
                    Pieces = [0x80]
                }
            );
        options.PollInterval = TimeSpan.FromSeconds(1);

        // Act
        var copying = sut.Handle(command, CancellationToken.None).AsTask();
        await clock.NextDelayAsync();
        clock.Advance(options.PollInterval);
        await copying;

        // Assert
        output.ToArray().Should().Equal(media.Content);
        mediator.Verify(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Theory, AutoMoqData]
    internal async Task Should_RespectGlobalPieceOffsets(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            FileOffset = media.Content.Length,
            PieceSize = media.Content.Length,
            Pieces = [0x40],
            Complete = false,
            DiskWritesSafe = true
        };
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert
        output.ToArray().Should().Equal(media.Content);
    }

    [Theory, AutoMoqData]
    internal async Task Should_CancelWithoutServingUnavailableBytes(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var options = command.Options;
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            FileOffset = 0,
            PieceSize = media.Content.Length,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);
        using var cancellation = new CancellationTokenSource();
        options.PollInterval = TimeSpan.FromSeconds(1);

        // Act
        var copying = sut.Handle(command, cancellation.Token).AsTask();
        cancellation.Cancel();
        var action = () => copying;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        output.Length.Should().Be(0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SwitchBackingFileDuringPlayback(
        InMemoryMedia media,
        string importedPath,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        media.SetupReads(files, importedPath);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            FileOffset = 0,
            PieceSize = media.Content.Length / 2,
            Pieces = [0x80],
            Complete = false,
            DiskWritesSafe = true
        };
        mediator.SetupSequence(x => x.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state).ReturnsAsync(
                state with
                {
                    Path = importedPath,
                    Complete = true
                }
            );

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert
        output.ToArray().Should().Equal(media.Content);
        files.Verify(value => value.ReadAsync(importedPath, It.IsAny<Memory<byte>>(), media.Content.Length / 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseUnsafeWriteCache(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            DiskWritesSafe = false
        };
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);

        // Act
        var action = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
        output.Length.Should().Be(0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_TimeoutWhenPiecesNeverArrive(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        [Frozen] ObservableFakeTimeProvider clock,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var options = command.Options;
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            FileOffset = 0,
            PieceSize = media.Content.Length,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);
        options.PollInterval = TimeSpan.FromMinutes(1);
        options.StallTimeout = TimeSpan.FromSeconds(5);

        // Act
        var copying = sut.Handle(command, CancellationToken.None).AsTask();
        var scheduled = await clock.NextDelayAsync();
        clock.Advance(options.StallTimeout - TimeSpan.FromMilliseconds(1));
        var completedBeforeDeadline = copying.IsCompleted;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var action = () => copying;

        // Assert
        scheduled.Should().Be(options.StallTimeout, "a poll cannot overshoot the stall deadline");
        completedBeforeDeadline.Should().BeFalse();
        await action.Should().ThrowAsync<TimeoutException>();
        output.Length.Should().Be(0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResetStallDeadlineAfterWritingBytes(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        [Frozen] ObservableFakeTimeProvider clock,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var options = command.Options;
        var output = (MemoryStream)command.Destination;
        media.SetupReads(files);
        var pieceSize = media.Content.Length / 2;
        var unavailable = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            FileOffset = 0,
            PieceSize = pieceSize,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        var firstPiece = unavailable with
        {
            Pieces = [0x80]
        };
        var reads = 0;
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++reads == 1 ? unavailable : firstPiece);
        options.PollInterval = TimeSpan.FromSeconds(4);
        options.StallTimeout = TimeSpan.FromSeconds(5);
        options.PrioritizeMissingPieces = false;

        // Act
        var copying = sut.Handle(command, CancellationToken.None).AsTask();
        await clock.NextDelayAsync();
        clock.Advance(options.PollInterval);
        await clock.NextDelayAsync();
        clock.Advance(options.PollInterval);
        var finalWait = await clock.NextDelayAsync();
        var completedBeforeDeadline = copying.IsCompleted;
        clock.Advance(finalWait);
        var action = () => copying;

        // Assert
        finalWait.Should().Be(options.StallTimeout - options.PollInterval);
        completedBeforeDeadline.Should().BeFalse();
        await action.Should().ThrowAsync<TimeoutException>();
        output.ToArray().Should().Equal(media.Content.Take(pieceSize));
    }

    [Theory, AutoMoqData]
    internal async Task Should_WaitForTheBackingFileToBecomeVisible(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        [Frozen] ObservableFakeTimeProvider clock,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        var options = command.Options;
        var output = (MemoryStream)command.Destination;
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            Complete = true,
            DiskWritesSafe = true
        };
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var attempts = 0;
        files.Setup(value => value.ReadAsync(media.Path, It.IsAny<Memory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Memory<byte> buffer, long offset, CancellationToken cancellationToken) =>
                ++attempts == 1 ? ValueTask.FromResult(0) : media.ReadAsync(buffer, offset, cancellationToken)
            );
        options.PollInterval = TimeSpan.FromSeconds(1);

        // Act
        var copying = sut.Handle(command, CancellationToken.None).AsTask();
        await clock.NextDelayAsync();
        clock.Advance(options.PollInterval);
        await copying;

        // Assert
        output.ToArray().Should().Equal(media.Content);
    }

    [Theory, AutoMoqData]
    internal async Task Should_IsolateTheSessionForEachCopy(
        InMemoryMedia media,
        ProgressiveReadState generated,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaFiles> files,
        CopyProgressiveMedia.Command command,
        CopyProgressiveMedia.Handler sut
    )
    {
        // Arrange
        command = command with
        {
            Offset = 0,
            Count = media.Content.Length
        };
        media.SetupReads(files);
        var state = generated with
        {
            Path = media.Path,
            Length = media.Content.Length,
            Complete = true,
            DiskWritesSafe = true
        };
        var sessions = new List<PlaybackSession>();
        mediator.Setup(value => value.Send(It.IsAny<IQuery<ProgressiveReadState>>(), It.IsAny<CancellationToken>()))
            .Callback((IQuery<ProgressiveReadState> query, CancellationToken _) => sessions.Add(((GetPlaybackState.Query)query).Playback))
            .Returns(ValueTask.FromResult(state));

        // Act
        await sut.Handle(command, CancellationToken.None);
        await sut.Handle(command, CancellationToken.None);

        // Assert
        sessions.Should().HaveCount(2);
        sessions[0].Should().NotBeSameAs(sessions[1]);
        sessions.Should().OnlyContain(session => ReferenceEquals(session.Entry, command.Projection));
        ((MemoryStream)command.Destination).ToArray().Should().Equal(media.Content.Concat(media.Content));
    }
}
