namespace TransmissionStreaming.Logic.Tests.Imports;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Moq;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Imports;
using Xunit;

public sealed class ResolveImportedFileTests
{
    [Theory, AutoMoqData]
    internal async Task Should_KeepCompletedBackingPath(ImportScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IProjectionPaths> paths,
        [Frozen] Mock<IMediaFiles> media, [Frozen] Mock<IArrClient> arr, ResolveImportedFile.Handler sut)
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = source.LocalPath;
        options.Setup(value => value.Get()).Returns(source.Options);
        paths.Setup(value => value.Map(source.LocalPath, source.Options.LocalMediaRoot, source.Options.LocalMediaRoot)).Returns(source.LocalPath);
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().Be(source.LocalPath);
        arr.Verify(value => value.FindImportedFileAsync(It.IsAny<ArrConnection>(), It.IsAny<MediaIdentity>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_MapVerifiedImport(ImportScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IProjectionPaths> paths,
        [Frozen] Mock<IMediaFiles> media, [Frozen] Mock<IArrClient> arr, ResolveImportedFile.Handler sut)
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = null;
        entry.Identity.Kind = MediaKind.Movie;
        options.Setup(value => value.Get()).Returns(source.Options);
        arr.Setup(value => value.FindImportedFileAsync(It.Is<ArrConnection>(connection => connection.Url == source.Options.RadarrUrl),
            entry.Identity, entry.Length, It.IsAny<CancellationToken>())).ReturnsAsync(source.RemotePath);
        paths.Setup(value => value.Map(source.RemotePath, source.Options.RemoteMediaRoot, source.Options.LocalMediaRoot)).Returns(source.LocalPath);
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(true);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().Be(source.LocalPath);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectChangedCompletedFile(ImportScenario source,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IProjectionPaths> paths,
        [Frozen] Mock<IMediaFiles> media, ResolveImportedFile.Handler sut)
    {
        // Arrange
        var entry = source.Query.Projection;
        entry.CompletedPath = source.LocalPath;
        options.Setup(value => value.Get()).Returns(source.Options);
        paths.Setup(value => value.Map(source.LocalPath, source.Options.LocalMediaRoot, source.Options.LocalMediaRoot)).Returns(source.LocalPath);
        media.Setup(value => value.HasLength(source.LocalPath, entry.Length)).Returns(false);

        // Act
        var result = await sut.Handle(source.Query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}

public sealed record ImportScenario(ResolveImportedFile.Query Query, StreamingOptions Options, string RemotePath, string LocalPath);
