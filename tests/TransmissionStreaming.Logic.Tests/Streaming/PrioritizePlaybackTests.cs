namespace TransmissionStreaming.Logic.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Streaming;
using Xunit;

public sealed class PrioritizePlaybackTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PrioritizeTheGlobalPieceContainingThePlaybackOffset(PrioritizePlayback.Command source, StreamingOptions configuration,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, PrioritizePlayback.Handler sut)
    {
        // Arrange
        var torrent = source.Playback.Torrent!;
        torrent.SequentialDownload = true;
        options.SetupGet(value => value.CurrentValue).Returns(configuration);
        client.Setup(value => value.SetSequentialStartAsync(It.IsAny<TransmissionConnection>(), source.Playback.Entry.TorrentHash, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var expectedPiece = (int)((torrent.GetFileOffset(source.Playback.Entry.FileIndex) + source.FileOffset) / torrent.PieceSize);

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        client.Verify(value => value.SetSequentialStartAsync(It.Is<TransmissionConnection>(connection => connection.Url == configuration.TransmissionUrl
                                                                                                         && connection.Username == configuration.TransmissionUsername &&
                                                                                                         connection.Password == configuration.TransmissionPassword),
            source.Playback.Entry.TorrentHash, expectedPiece, CancellationToken.None), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SkipTorrentsWithoutSequentialMode(PrioritizePlayback.Command source,
        [Frozen] Mock<ITransmissionClient> client, PrioritizePlayback.Handler sut)
    {
        // Arrange
        source.Playback.Torrent!.SequentialDownload = false;

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        client.Verify(value => value.SetSequentialStartAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SkipPlaybackWithoutTorrentMetadata(PrioritizePlayback.Command source,
        [Frozen] Mock<ITransmissionClient> client, PrioritizePlayback.Handler sut)
    {
        // Arrange
        source.Playback.Torrent = null;

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        client.Verify(value => value.SetSequentialStartAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SkipAnInvalidFileIndex(PrioritizePlayback.Command source,
        [Frozen] Mock<ITransmissionClient> client, PrioritizePlayback.Handler sut)
    {
        // Arrange
        source.Playback.Torrent!.SequentialDownload = true;
        source.Playback.Entry.FileIndex = source.Playback.Torrent.Files.Count;

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        client.Verify(value => value.SetSequentialStartAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<Projection>(composer => composer.With(value => value.FileIndex, 0));
        return fixture;
    });
}
