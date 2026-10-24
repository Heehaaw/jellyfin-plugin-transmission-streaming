namespace TransmissionStreaming.Infrastructure.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure.Streaming;
using Moq;
using Xunit;

public sealed class PlaybackSourceFactoryTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ThrottlePersistedCompletionLookups(Projection entry, StreamingOptions configuration, TorrentSnapshot torrent,
        TransmissionSessionSnapshot session, [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<ITransmissionClient> transmission, [Frozen] Mock<TimeProvider> clock, PlaybackSourceFactory sut)
    {
        // Arrange
        entry.FileIndex = 0;
        entry.CompletedPath = null;
        torrent.Files[0].Length = entry.Length;
        var timestamp = 0L;
        var interval = TimeSpan.FromMilliseconds(500);
        clock.SetupGet(value => value.TimestampFrequency).Returns(TimeSpan.TicksPerSecond);
        clock.Setup(value => value.GetTimestamp()).Returns(() => timestamp);
        options.Setup(value => value.Get()).Returns(configuration);
        store.Setup(value => value.Get(entry.Id)).Returns(entry);
        transmission.Setup(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), entry.TorrentHash, It.IsAny<CancellationToken>())).ReturnsAsync(torrent);
        transmission.Setup(value => value.GetSessionAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        // Act
        var source = sut.Create(entry);
        await source.GetStateAsync(CancellationToken.None);
        await source.GetStateAsync(CancellationToken.None);
        timestamp = interval.Ticks - 1;
        await source.GetStateAsync(CancellationToken.None);
        var callsBeforeBoundary = store.Invocations.Count;
        timestamp = interval.Ticks;
        await source.GetStateAsync(CancellationToken.None);

        // Assert
        callsBeforeBoundary.Should().Be(1);
        store.Verify(value => value.Get(entry.Id), Times.Exactly(2));
        clock.Verify(value => value.GetUtcNow(), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Register<TimeProvider>(() => fixture.Create<Mock<TimeProvider>>().Object);
        fixture.Customize<TorrentSnapshot>(composer => composer.With(value => value.Pieces, Convert.ToBase64String(fixture.Create<byte[]>())));
        return fixture;
    });
}
