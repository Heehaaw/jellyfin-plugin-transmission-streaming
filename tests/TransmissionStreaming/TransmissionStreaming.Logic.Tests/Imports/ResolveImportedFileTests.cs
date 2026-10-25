namespace TransmissionStreaming.Logic.Tests.Imports;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Mediator;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Imports;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class ResolveImportedFileTests
{
    [Theory, AutoMoqData]
    internal async Task Should_KeepCompletedBackingPath(
        ImportScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaFiles> media,
        [Frozen] Mock<IMediator> paths,
        ResolveImportedFile.Handler sut
    )
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = source.LocalPath;
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        paths.Setup(value => value.Send(It.Is<ValidateLocalMediaPath.Query>(request => request.Path == source.LocalPath), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string?>(source.LocalPath));
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().Be(source.LocalPath);
        paths.Verify(value => value.Send(It.Is<FindArrImportedFile.Query>(query => true), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_MapVerifiedImport(
        ImportScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaFiles> media,
        [Frozen] Mock<IMediator> paths,
        ResolveImportedFile.Handler sut
    )
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = null;
        entry.Identity.Kind = MediaKind.Movie;
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        paths.Setup(value => value.Send(
                query: It.Is<FindArrImportedFile.Query>(query =>
                    (query.Connection.Url == source.Options.RadarrUrl) && (query.Identity == entry.Identity) && (query.ExpectedSize == entry.Length)
                ),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(source.RemotePath);
        paths.Setup(value => value.Send(
                query: It.Is<MapServicePath.Query>(request => request.Service == MediaService.Radarr && request.Path == source.RemotePath),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).Returns(ValueTask.FromResult<string?>(source.LocalPath));
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().Be(source.LocalPath);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectChangedCompletedFile(
        ImportScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> paths,
        [Frozen] Mock<IMediaFiles> media,
        ResolveImportedFile.Handler sut
    )
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = source.LocalPath;
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        paths.Setup(value => value.Send(It.Is<ValidateLocalMediaPath.Query>(request => request.Path == source.LocalPath), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string?>(source.LocalPath));
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(false);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResolveEpisodesThroughSonarr(
        ImportScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaFiles> media,
        [Frozen] Mock<IMediator> paths,
        ResolveImportedFile.Handler sut
    )
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = null;
        entry.Identity.Kind = MediaKind.Episode;
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        paths.Setup(value => value.Send(
                query: It.Is<FindArrImportedFile.Query>(query =>
                    (query.Connection.Url == source.Options.SonarrUrl && query.Connection.ApiKey == source.Options.SonarrApiKey) && (query.Identity == entry.Identity) &&
                    (query.ExpectedSize == entry.Length)
                ),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(source.RemotePath);
        paths.Setup(value => value.Send(
                query: It.Is<MapServicePath.Query>(request => request.Service == MediaService.Sonarr && request.Path == source.RemotePath),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).Returns(ValueTask.FromResult<string?>(source.LocalPath));
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().Be(source.LocalPath);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAnImportWithDifferentLength(
        ImportScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaFiles> media,
        [Frozen] Mock<IMediator> paths,
        ResolveImportedFile.Handler sut
    )
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = null;
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        paths.Setup(value => value.Send(
                query: It.Is<FindArrImportedFile.Query>(query => (query.Identity == entry.Identity) && (query.ExpectedSize == entry.Length)),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(source.RemotePath);
        paths.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Path == source.RemotePath), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string?>(source.LocalPath));
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(false);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotCallAnUnconfiguredArr(
        ImportScenario source,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> paths,
        ResolveImportedFile.Handler sut
    )
    {
        // Arrange
        source.Query.Projection.CompletedPath = null;
        options.SetupGet(value => value.CurrentValue).Returns(
            source.Options with
            {
                RadarrApiKey = "",
                SonarrApiKey = ""
            }
        );

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        paths.Verify(value => value.Send(It.Is<FindArrImportedFile.Query>(query => true), It.IsAny<CancellationToken>()), Times.Never);
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
            return fixture;
        }
    );
}

public sealed record ImportScenario(ResolveImportedFile.Query Query, StreamingOptions Options, string RemotePath, string LocalPath);
