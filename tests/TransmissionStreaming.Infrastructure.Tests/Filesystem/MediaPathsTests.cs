namespace TransmissionStreaming.Infrastructure.Tests.Filesystem;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Projection;
using global::TransmissionStreaming.Infrastructure.Filesystem;
using Xunit;

public sealed class MediaPathsTests
{
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });

    [Theory, AutoMoqData]
    internal void Should_MapAFileInsideTheApprovedRoot(string name, TemporaryDirectory directory, MediaPaths sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";
        var expectedPath = Path.Combine(root.Path, name);

        // Act
        var result = sut.Map(remoteRoot + "/" + name, remoteRoot, root.Path);

        // Assert
        result.Should().Be(expectedPath);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectParentTraversalOutsideTheRemoteRoot(string name, TemporaryDirectory directory, MediaPaths sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";

        // Act
        var action = () => sut.Map(remoteRoot + "/../" + name, remoteRoot, root.Path);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAPathThatOnlySharesTheRootPrefix(string name, TemporaryDirectory directory, MediaPaths sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";

        // Act
        var action = () => sut.Map(remoteRoot + "-outside/" + name, remoteRoot, root.Path);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectSymbolicLinkFiles(string name, TemporaryDirectory directory, MediaPaths sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";
        var link = Path.Combine(root.Path, name);
        File.CreateSymbolicLink(link, Path.Combine(Path.GetTempPath(), name));

        // Act
        var action = () => sut.Map(remoteRoot + "/" + name, remoteRoot, root.Path);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectSymbolicLinkDirectories(string name, TemporaryDirectory directory, MediaPaths sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";
        var link = Path.Combine(root.Path, name);
        Directory.CreateSymbolicLink(link, Path.GetTempPath());

        // Act
        var action = () => sut.Map(remoteRoot + "/" + name + "/movie.mkv", remoteRoot, root.Path);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_KeepMovieIdentityWhenPresentationChanges(Projection projection, MediaPaths sut)
    {
        // Arrange
        projection.Identity.Kind = MediaKind.Movie;
        var expectedId = sut.Id(projection.Identity);
        projection.Identity.Title += " [Downloading]";
        projection.Progress += 0.1;

        // Act
        var result = sut.Id(projection.Identity);

        // Assert
        result.Should().Be(expectedId);
    }

    [Theory, AutoMoqData]
    internal void Should_KeepEpisodeIdentityWhenPresentationChanges(MediaIdentity identity, MediaPaths sut)
    {
        // Arrange
        identity.Kind = MediaKind.Episode;
        var expectedId = sut.Id(identity);
        identity.Title += " [Preparing stream]";
        identity.EpisodeTitle += " [Downloading]";

        // Act
        var result = sut.Id(identity);

        // Assert
        result.Should().Be(expectedId);
    }
}
