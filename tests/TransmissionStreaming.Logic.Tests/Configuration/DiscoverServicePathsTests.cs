namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Mediator;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class DiscoverServicePathsTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PairALoneArrRootWithItsLibraryType(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaLibraryLocations> libraries, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var query = source.Query with { Service = MediaService.Radarr };
        var root = source.Paths.LibraryRoots[0];
        var local = source.LocalFolders[0];
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Paths);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { PathMappings = [] });
        libraries.Setup(value => value.GetServicePaths(query.Service)).Returns([local]);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Paths.Should().BeSameAs(source.Paths);
        result.Mappings.Should().ContainSingle().Which.RemoteRoot.Should().Be(root);
        result.Mappings[0].LocalRoot.Should().Be(local);
        result.Mappings[0].Service.Should().Be(query.Service);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveASavedManualOverride(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var mapping = source.Options.PathMappings[0];
        mapping.Service = MediaService.Sonarr;
        var query = source.Query with { Service = mapping.Service };
        var paths = source.Paths with { LibraryRoots = [mapping.RemoteRoot] };
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(paths);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Mappings.Should().ContainSingle().Which.Should().BeEquivalentTo(mapping);
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveAmbiguousLocalMountsForManualEntry(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaLibraryLocations> libraries, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var query = source.Query with { Service = MediaService.Sonarr };
        var candidates = new[] { source.LocalFolders[0], source.LocalFolders[0] + "/other" };
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(source.Paths);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { PathMappings = [] });
        libraries.Setup(value => value.GetServicePaths(query.Service)).Returns(candidates);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Mappings.Should().ContainSingle().Which.LocalRoot.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal async Task Should_CombineTransmissionDownloadAndIncompleteFolders(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> paths, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var query = source.Query with { Service = MediaService.Transmission };
        var upstream = source.Paths with { DownloadDirectories = [source.Paths.DownloadDirectory!] };
        var expected = new[] { upstream.DownloadDirectory, upstream.IncompleteDirectory };
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(upstream);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { PathMappings = [] });
        paths.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Service == query.Service && request.Path == upstream.DownloadDirectory!), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(source.LocalFolders[0]));

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Mappings.Select(mapping => mapping.RemoteRoot).Should().BeEquivalentTo(expected);
        result.Mappings.Single(mapping => mapping.RemoteRoot == upstream.DownloadDirectory).LocalRoot.Should().Be(source.LocalFolders[0]);
    }

    [Theory, AutoMoqData]
    internal async Task Should_OmitAChildCoveredByItsParent(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaPathMapper> physicalPaths, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var query = source.Query with { Service = MediaService.Transmission };
        var parent = source.Paths.DownloadDirectory!;
        var suffix = source.Paths.IncompleteDirectory!;
        var child = parent + suffix;
        var localParent = source.LocalFolders[0];
        var localChild = localParent + suffix;
        var paths = source.Paths with { DownloadDirectories = [parent, child], IncompleteDirectory = null };
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(paths);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { PathMappings = [] });
        mediator.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Path == parent), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(localParent));
        mediator.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Path == child), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(localChild));
        physicalPaths.Setup(value => value.Map(child, parent, localParent)).Returns(localChild);
    
        // Act
        var result = await sut.Handle(query, CancellationToken.None);
    
        // Assert
        result.Mappings.Should().ContainSingle().Which.RemoteRoot.Should().Be(parent);
        result.Paths.Should().BeSameAs(paths, "the raw service response still explains where suggestions came from");
    }
    
    [Theory, AutoMoqData]
    internal async Task Should_KeepASavedChildWithADifferentLocalTarget(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> mediator, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var query = source.Query with { Service = MediaService.Transmission };
        var parent = source.Paths.DownloadDirectory!;
        var child = parent + source.Paths.IncompleteDirectory!;
        var saved = source.Options.PathMappings[0];
        saved.Service = query.Service;
        saved.RemoteRoot = child;
        var paths = source.Paths with { DownloadDirectories = [parent, child], IncompleteDirectory = null };
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(paths);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options);
        mediator.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Path == parent), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(source.LocalFolders[0]));
    
        // Act
        var result = await sut.Handle(query, CancellationToken.None);
    
        // Assert
        result.Mappings.Should().HaveCount(paths.DownloadDirectories.Count);
        result.Mappings.Single(mapping => mapping.RemoteRoot == child).LocalRoot.Should().Be(saved.LocalRoot);
    }
    
    [Theory, AutoMoqData]
    internal async Task Should_KeepAChildWhenTheParentCannotTranslateIt(DiscoverySource source,
        [Frozen] Mock<IServicePathDiscovery> discovery, [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaPathMapper> physicalPaths, DiscoverServicePaths.Handler sut)
    {
        // Arrange
        var query = source.Query with { Service = MediaService.Transmission };
        var parent = source.Paths.DownloadDirectory!;
        var suffix = source.Paths.IncompleteDirectory!;
        var child = parent + suffix;
        var localParent = source.LocalFolders[0];
        var paths = source.Paths with { DownloadDirectories = [parent, child], IncompleteDirectory = null };
        discovery.Setup(value => value.DiscoverAsync(query.Service, query.Connection, It.IsAny<CancellationToken>())).ReturnsAsync(paths);
        options.SetupGet(value => value.CurrentValue).Returns(source.Options with { PathMappings = [] });
        mediator.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Path == parent), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(localParent));
        mediator.Setup(value => value.Send(It.Is<MapServicePath.Query>(request => request.Path == child), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(localParent + suffix));
        physicalPaths.Setup(value => value.Map(child, parent, localParent)).Returns((string?)null);
    
        // Act
        var result = await sut.Handle(query, CancellationToken.None);
    
        // Assert
        result.Mappings.Should().HaveCount(paths.DownloadDirectories.Count);
    }
    
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        // All discovered folders must be absolute; generated labels keep different mounts distinct.
        fixture.Register(() => "/" + fixture.Create<Guid>().ToString("N"));
        fixture.Customize<Mock<IMediator>>(composer => composer.Do(mock =>
            mock.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null))));
        return fixture;
    });
}

public sealed record DiscoverySource(DiscoverServicePaths.Query Query, DiscoveredServicePaths Paths, StreamingOptions Options, string[] LocalFolders);
