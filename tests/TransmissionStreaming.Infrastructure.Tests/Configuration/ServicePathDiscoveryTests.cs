namespace TransmissionStreaming.Infrastructure.Tests.Configuration;

using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure.Configuration;
using Moq;
using Xunit;

public sealed class ServicePathDiscoveryTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReportDefaultAndPerTorrentDownloadDirectories(
        ServiceConnection connection,
        TransmissionSessionSnapshot session,
        TorrentSnapshot torrent,
        [Frozen] Mock<ITransmissionClient> transmission,
        ServicePathDiscovery sut
    )
    {
        // Arrange
        session.IncompleteDirectoryEnabled = true;
        transmission.Setup(value => value.GetSessionAsync(
                connection: It.Is<TransmissionConnection>(server => server.Url == connection.Url &&
                                                                    server.Username == connection.Username && server.Password == connection.Password
                ),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(session);
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([torrent, torrent]);

        // Act
        var result = await sut.DiscoverAsync(MediaService.Transmission, connection, CancellationToken.None);

        // Assert
        result.DownloadDirectory.Should().Be(session.DownloadDirectory);
        result.IncompleteDirectory.Should().Be(session.IncompleteDirectory);
        result.DownloadDirectories.Should().Equal(session.DownloadDirectory, torrent.DownloadDirectory);
        result.LibraryRoots.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal async Task Should_OmitDisabledIncompleteDirectory(
        ServiceConnection connection,
        TransmissionSessionSnapshot session,
        [Frozen] Mock<ITransmissionClient> transmission,
        ServicePathDiscovery sut
    )
    {
        // Arrange
        session.IncompleteDirectoryEnabled = false;
        transmission.Setup(value => value.GetSessionAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);
        transmission.Setup(value => value.GetTorrentsAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        var result = await sut.DiscoverAsync(MediaService.Transmission, connection, CancellationToken.None);

        // Assert
        result.IncompleteDirectory.Should().BeNull();
        result.DownloadDirectories.Should().Equal(session.DownloadDirectory);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReadEachArrServicesOwnLibraryRoots(
        ServiceConnection connection,
        string[] roots,
        [Frozen] Mock<IArrClient> arr,
        [Frozen] Mock<ITransmissionClient> transmission,
        ServicePathDiscovery sut
    )
    {
        // Arrange
        arr.Setup(value => value.GetRootFoldersAsync(
                connection: It.Is<ArrConnection>(server => server.Url == connection.Url && server.ApiKey == connection.ApiKey),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(roots);

        foreach (var service in new[]
                 {
                     MediaService.Radarr, MediaService.Sonarr
                 })
        {
            // Act
            var result = await sut.DiscoverAsync(service, connection, CancellationToken.None);

            // Assert
            result.LibraryRoots.Should().Equal(roots);
            result.DownloadDirectory.Should().BeNull();
            result.DownloadDirectories.Should().BeEmpty();
        }

        transmission.Verify(value => value.GetSessionAsync(It.IsAny<TransmissionConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PropagateServiceFailure(ServiceConnection connection, [Frozen] Mock<IArrClient> arr, ServicePathDiscovery sut)
    {
        // Arrange
        var failure = new HttpRequestException();
        arr.Setup(value => value.GetRootFoldersAsync(It.IsAny<ArrConnection>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        // Act
        var action = () => sut.DiscoverAsync(MediaService.Radarr, connection, CancellationToken.None);

        // Assert
        (await action.Should().ThrowAsync<HttpRequestException>()).Which.Should().BeSameAs(failure);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectUnknownServices(ServiceConnection connection, ServicePathDiscovery sut)
    {
        // Arrange
        var unsupported = (MediaService)int.MaxValue;

        // Act
        var action = () => sut.DiscoverAsync(unsupported, connection, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
