namespace TransmissionStreaming.Infrastructure.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using Filesystem;
using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure.Filesystem;
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

    [Theory, AutoMoqData]
    internal async Task Should_FindRenamedIncompleteFiles([Frozen] SourceScenario source, TemporaryDirectory directory,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        using var root = directory;
        source.Mappings[0] = source.Mappings[0] with { RemoteRoot = "/data", LocalRoot = root.Path };
        source.Torrent.DownloadDirectory = "/data/completed";
        source.Torrent.SequentialDownload = true;
        source.Session.IncompleteDirectoryEnabled = true;
        source.Session.IncompleteDirectory = "/data/incomplete";
        source.Session.RenamePartialFiles = true;
        var incomplete = Directory.CreateDirectory(Path.Combine(root.Path, "incomplete")).FullName;
        var partial = Path.Combine(incomplete, source.Torrent.Files[0].Name + ".part");
        File.WriteAllBytes(partial, new byte[source.Torrent.Files[0].Length]);
        client.Setup(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>())).ReturnsAsync(source.Torrent);
        client.Setup(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Session);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        // Act
        var state = await sut.GetStateAsync(CancellationToken.None);

        // Assert
        state.Path.Should().Be(partial);
        state.DiskWritesSafe.Should().BeTrue();
        state.Complete.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_UseAnImportedFileWithoutContactingTheRemovedTorrent([Frozen] SourceScenario source, TemporaryDirectory directory,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        using var root = directory;
        source.Mappings[0] = source.Mappings[0] with { RemoteRoot = "/data", LocalRoot = root.Path };
        var imported = Path.Combine(root.Path, source.Torrent.Files[0].Name);
        File.WriteAllBytes(imported, new byte[source.Torrent.Files[0].Length]);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync(imported);

        // Act
        var state = await sut.GetStateAsync(CancellationToken.None);

        // Assert
        state.Path.Should().Be(imported);
        state.Complete.Should().BeTrue();
        client.Verify(value => value.GetTorrentAsync(It.IsAny<TransmissionConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PrioritizeThePieceContainingThePlaybackOffset([Frozen] SourceScenario source, long offset,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        source.Torrent.SequentialDownload = true;
        var expectedPiece = (int)(offset / source.Torrent.PieceSize);
        client.Setup(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>())).ReturnsAsync(source.Torrent);
        client.Setup(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Session);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        await sut.GetStateAsync(CancellationToken.None);

        // Act
        await sut.PrioritizeAsync(offset, CancellationToken.None);

        // Assert
        client.Verify(value => value.SetSequentialStartAsync(source.Connection, source.Torrent.Hash, expectedPiece, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseAChangedReleaseLength([Frozen] SourceScenario source,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        source.Torrent.Files[0].Length++;
        client.Setup(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>())).ReturnsAsync(source.Torrent);
        client.Setup(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Session);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        // Act
        var action = () => sut.GetStateAsync(CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseMalformedPieceMaps([Frozen] SourceScenario source,
        [Frozen] Mock<ITransmissionClient> client, [Frozen] Mock<Func<CancellationToken, Task<string?>>> completion, TransmissionFileSource sut)
    {
        // Arrange
        source.Torrent.Pieces = "!";
        client.Setup(value => value.GetTorrentAsync(source.Connection, source.Torrent.Hash, It.IsAny<CancellationToken>())).ReturnsAsync(source.Torrent);
        client.Setup(value => value.GetSessionAsync(source.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Session);
        completion.Setup(value => value(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        // Act
        var action = () => sut.GetStateAsync(CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }

    internal sealed record SourceScenario(TransmissionConnection Connection, TorrentSnapshot Torrent, TransmissionSessionSnapshot Session, List<PathMapping> Mappings);

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<TorrentSnapshot>(composer => composer.With(value => value.Pieces, Convert.ToBase64String(fixture.Create<byte[]>())));
        fixture.Register(() =>
        {
            var source = fixture.Create<SourceScenario>();
            return new TransmissionFileSource(fixture.Create<Mock<ITransmissionClient>>().Object, fixture.Create<Mock<TimeProvider>>().Object,
                source.Connection, source.Torrent.Hash, 0, source.Mappings, source.Torrent.Files[0].Length,
                fixture.Create<Mock<Func<CancellationToken, Task<string?>>>>().Object);
        });
        return fixture;
    });
}
