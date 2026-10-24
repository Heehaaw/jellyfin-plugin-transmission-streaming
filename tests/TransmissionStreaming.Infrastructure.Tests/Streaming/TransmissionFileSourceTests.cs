namespace TransmissionStreaming.Infrastructure.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure.Streaming;
using Moq;
using Xunit;

public sealed class TransmissionFileSourceTests
{
    [Theory, AutoMoqData]
    internal async Task Should_RefreshOnlyAtMonotonicCacheBoundary([Frozen] SourceScenario source,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<TimeProvider> clock,
        [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        var timestamp = 0L;
        var interval = TimeSpan.FromMilliseconds(500);
        clock.SetupGet(value => value.TimestampFrequency).Returns(TimeSpan.TicksPerSecond);
        clock.Setup(value => value.GetTimestamp()).Returns(() => timestamp);
        client.Setup(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>())).ReturnsAsync(source.Torrent);
        client.Setup(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Session);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        // Act
        var initial = await sut.GetStateAsync(CancellationToken.None);
        for (var index = 0; index < 10; index++)
        {
            await sut.GetStateAsync(CancellationToken.None);
        }
        timestamp = interval.Ticks - 1;
        await sut.GetStateAsync(CancellationToken.None);
        // UTC jumps cannot expire a monotonic cache; no wall-clock read is allowed.
        clock.Setup(value => value.GetUtcNow()).Returns(DateTimeOffset.MaxValue);
        var beforeBoundary = client.Invocations.Count;
        timestamp = interval.Ticks;
        await sut.GetStateAsync(CancellationToken.None);

        // Assert
        initial.Length.Should().Be(source.Torrent.Files[0].Length);
        beforeBoundary.Should().Be(2, "the first refresh makes one torrent and one session request");
        client.Verify(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>()), Times.Exactly(2));
        client.Verify(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>()), Times.Exactly(2));
        completion.Verify(value => value(It.IsAny<CancellationToken>()), Times.Exactly(2));
        clock.Verify(value => value.GetUtcNow(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_CacheMissingTorrentBetweenRefreshes([Frozen] SourceScenario source,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<TimeProvider> clock,
        [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        clock.SetupGet(value => value.TimestampFrequency).Returns(TimeSpan.TicksPerSecond);
        clock.Setup(value => value.GetTimestamp()).Returns(0);
        client.Setup(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>())).ReturnsAsync((TorrentSnapshot?)null);
        client.Setup(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Session);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        // Act
        var initial = await sut.GetStateAsync(CancellationToken.None);
        await sut.GetStateAsync(CancellationToken.None);
        await sut.GetStateAsync(CancellationToken.None);

        // Assert
        initial.Path.Should().BeNull();
        initial.Length.Should().Be(source.Torrent.Files[0].Length);
        client.Verify(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>()), Times.Once);
        completion.Verify(value => value(It.IsAny<CancellationToken>()), Times.Once);
    }

    internal sealed record SourceScenario(TransmissionConnection Connection, TorrentSnapshot Torrent, TransmissionSessionSnapshot Session);

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<TorrentSnapshot>(composer => composer.With(value => value.Pieces, Convert.ToBase64String(fixture.Create<byte[]>())));
        fixture.Register(() =>
        {
            var source = fixture.Create<SourceScenario>();
            return new TransmissionFileSource(fixture.Create<Mock<ITransmissionClient>>().Object, fixture.Create<Mock<TimeProvider>>().Object,
                source.Connection, source.Torrent.Hash, 0, [], source.Torrent.Files[0].Length,
                fixture.Create<Mock<Func<CancellationToken, Task<string?>>>>().Object);
        });
        return fixture;
    });
}
