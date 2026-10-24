namespace TransmissionStreaming.Logic.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Streaming;
using Xunit;

public sealed class StreamMediaTests
{
    [Theory, AutoMoqData]
    internal async Task Should_KeepCompletedPlaybackIndependentOfTransmission(PlaybackScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IPlaybackSourceFactory> sources, [Frozen] Mock<IProgressiveReader> reader,
        [Frozen] Mock<IStreamActivity> activity, StreamMedia.Handler sut)
    {
        // Arrange
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true, PrioritizePlayback = true });
        sources.Setup(value => value.Create(source.Command.Projection)).Returns(source.Reader);
        activity.Setup(value => value.Open(source.Command.Projection.Id)).Returns(source.Lease);
        reader.Setup(value => value.CopyAsync(source.Reader, source.Command.Destination, source.Command.Offset,
            source.Command.Count, It.IsAny<ProgressiveReadOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(value => value.EnableSequentialAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        reader.Verify(value => value.CopyAsync(source.Reader, source.Command.Destination, source.Command.Offset,
            source.Command.Count, It.IsAny<ProgressiveReadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Get(source.Lease).Verify(value => value.Dispose(), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_EnableSequentialForIncompletePlayback(PlaybackScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<ITransmissionClient> transmission,
        [Frozen] Mock<IPlaybackSourceFactory> sources, [Frozen] Mock<IProgressiveReader> reader,
        [Frozen] Mock<IStreamActivity> activity, StreamMedia.Handler sut)
    {
        // Arrange
        source.Command.Projection.CompletedPath = null;
        options.Setup(value => value.Get()).Returns(source.Options with { Enabled = true, PrioritizePlayback = true });
        sources.Setup(value => value.Create(source.Command.Projection)).Returns(source.Reader);
        activity.Setup(value => value.Open(source.Command.Projection.Id)).Returns(source.Lease);
        transmission.Setup(value => value.EnableSequentialAsync(It.IsAny<TransmissionConnection>(), source.Command.Projection.TorrentHash,
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        reader.Setup(value => value.CopyAsync(source.Reader, source.Command.Destination, source.Command.Offset,
            source.Command.Count, It.IsAny<ProgressiveReadOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await sut.Handle(source.Command, CancellationToken.None);

        // Assert
        transmission.Verify(value => value.EnableSequentialAsync(It.Is<TransmissionConnection>(connection => connection.Url == source.Options.TransmissionUrl),
            source.Command.Projection.TorrentHash, It.IsAny<CancellationToken>()), Times.Once);
        Mock.Get(source.Lease).Verify(value => value.Dispose(), Times.Once);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Register<Stream>(() => new MemoryStream());
        return fixture;
    });
}

public sealed record PlaybackScenario(StreamMedia.Command Command, StreamingOptions Options, IProgressiveSource Reader, IDisposable Lease);
