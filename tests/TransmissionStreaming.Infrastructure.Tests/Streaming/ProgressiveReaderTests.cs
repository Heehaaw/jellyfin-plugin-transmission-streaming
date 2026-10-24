namespace TransmissionStreaming.Infrastructure.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Infrastructure.Streaming;
using Moq;
using Xunit;

public sealed class ProgressiveReaderTests
{
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Register(() => new MemoryStream());
        fixture.Customize<ProgressiveReadOptions>(composer => composer.With(value => value.BufferSize, 4096));
        fixture.Register(() => new ObservableFakeTimeProvider());
        fixture.Register<TimeProvider>(() => fixture.Create<ObservableFakeTimeProvider>());
        return fixture;
    });

    [Theory, AutoMoqData]
    internal async Task Should_WaitForVerifiedPieces(
        TemporaryMedia media, ProgressiveReadState generated,
        [Frozen] Mock<IProgressiveSource> source, [Frozen] ObservableFakeTimeProvider clock, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        var state = generated with
        {
            Path = file.Path,
            Length = file.Content.Length,
            FileOffset = 0,
            PieceSize = file.Content.Length,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        source.SetupSequence(x => x.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state).ReturnsAsync(state with { Pieces = [0x80] });
        var options = new ProgressiveReadOptions { PollInterval = TimeSpan.FromSeconds(1) };

        // Act
        var copying = sut.CopyAsync(source.Object, output, 0, file.Content.Length, options);
        await clock.NextDelayAsync();
        clock.Advance(options.PollInterval);
        await copying;

        // Assert
        output.ToArray().Should().Equal(file.Content);
        source.Verify(x => x.GetStateAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Theory, AutoMoqData]
    internal async Task Should_RespectGlobalPieceOffsets(
        TemporaryMedia media, ProgressiveReadState generated,
        [Frozen] Mock<IProgressiveSource> source, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        var state = generated with
        {
            Path = file.Path,
            Length = file.Content.Length,
            FileOffset = file.Content.Length,
            PieceSize = file.Content.Length,
            Pieces = [0x40],
            Complete = false,
            DiskWritesSafe = true
        };
        source.Setup(x => x.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        // Act
        await sut.CopyAsync(source.Object, output, 0, file.Content.Length, new ProgressiveReadOptions());

        // Assert
        output.ToArray().Should().Equal(file.Content);
    }

    [Theory, AutoMoqData]
    internal async Task Should_CancelWithoutServingUnavailableBytes(
        TemporaryMedia media, ProgressiveReadState generated,
        [Frozen] Mock<IProgressiveSource> source, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        var state = generated with
        {
            Path = file.Path,
            Length = file.Content.Length,
            FileOffset = 0,
            PieceSize = file.Content.Length,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        source.Setup(x => x.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        using var cancellation = new CancellationTokenSource();
        var options = new ProgressiveReadOptions();

        // Act
        var copying = sut.CopyAsync(source.Object, output, 0, file.Content.Length, options, cancellation.Token);
        cancellation.Cancel();
        var action = () => copying;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        output.Length.Should().Be(0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SwitchBackingFileDuringPlayback(
        TemporaryMedia media, TemporaryMedia completed, ProgressiveReadState generated,
        [Frozen] Mock<IProgressiveSource> source, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        using var imported = completed;
        await File.WriteAllBytesAsync(imported.Path, file.Content);
        var state = generated with
        {
            Path = file.Path,
            Length = file.Content.Length,
            FileOffset = 0,
            PieceSize = file.Content.Length / 2,
            Pieces = [0x80],
            Complete = false,
            DiskWritesSafe = true
        };
        source.SetupSequence(x => x.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state).ReturnsAsync(state with { Path = imported.Path, Complete = true });

        // Act
        await sut.CopyAsync(source.Object, output, 0, file.Content.Length, new ProgressiveReadOptions());

        // Assert
        output.ToArray().Should().Equal(file.Content);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseUnsafeWriteCache(
        TemporaryMedia media, ProgressiveReadState generated,
        [Frozen] Mock<IProgressiveSource> source, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        var state = generated with { Path = file.Path, Length = file.Content.Length, DiskWritesSafe = false };
        source.Setup(x => x.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        // Act
        var action = () => sut.CopyAsync(source.Object, output, 0, file.Content.Length, new ProgressiveReadOptions());

        // Assert
        await action.Should().ThrowAsync<IOException>();
        output.Length.Should().Be(0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_TimeoutWhenPiecesNeverArrive(
        TemporaryMedia media, ProgressiveReadState generated,
        [Frozen] Mock<IProgressiveSource> source, [Frozen] ObservableFakeTimeProvider clock, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        var state = generated with
        {
            Path = file.Path,
            Length = file.Content.Length,
            FileOffset = 0,
            PieceSize = file.Content.Length,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        source.Setup(x => x.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var options = new ProgressiveReadOptions { PollInterval = TimeSpan.FromMinutes(1), StallTimeout = TimeSpan.FromSeconds(5) };

        // Act
        var copying = sut.CopyAsync(source.Object, output, 0, file.Content.Length, options);
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
    internal async Task Should_ResetStallDeadlineAfterWritingBytes(TemporaryMedia media, ProgressiveReadState generated, ProgressiveReadOptions options,
        [Frozen] Mock<IProgressiveSource> source, [Frozen] ObservableFakeTimeProvider clock, ProgressiveReader sut, MemoryStream output)
    {
        // Arrange
        using var file = media;
        var pieceSize = file.Content.Length / 2;
        var unavailable = generated with
        {
            Path = file.Path,
            Length = file.Content.Length,
            FileOffset = 0,
            PieceSize = pieceSize,
            Pieces = [0],
            Complete = false,
            DiskWritesSafe = true
        };
        var firstPiece = unavailable with { Pieces = [0x80] };
        var reads = 0;
        source.Setup(value => value.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++reads == 1 ? unavailable : firstPiece);
        options.PollInterval = TimeSpan.FromSeconds(4);
        options.StallTimeout = TimeSpan.FromSeconds(5);
        options.PrioritizeMissingPieces = false;

        // Act
        var copying = sut.CopyAsync(source.Object, output, 0, file.Content.Length, options);
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
        output.ToArray().Should().Equal(file.Content.Take(pieceSize));
    }

}
