namespace TransmissionStreaming.Logic.Tests.Projection;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Projection;
using Xunit;

public sealed class BuildProjectionPathsTests
{
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

    [Theory, AutoMoqData]
    internal async Task Should_KeepMovieIdentityWhenPresentationChanges(Projection projection, BuildProjectionPaths.Handler sut)
    {
        // Arrange
        projection.Identity.Kind = MediaKind.Movie;
        var expectedId = (await sut.Handle(new BuildProjectionPaths.Query(string.Empty, projection.Identity), CancellationToken.None)).Id;
        projection.Identity.Title += " [Downloading]";
        projection.Progress += 0.1;

        // Act
        var result = (await sut.Handle(new BuildProjectionPaths.Query(string.Empty, projection.Identity), CancellationToken.None)).Id;

        // Assert
        result.Should().Be(expectedId);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepEpisodeIdentityWhenPresentationChanges(MediaIdentity identity, BuildProjectionPaths.Handler sut)
    {
        // Arrange
        identity.Kind = MediaKind.Episode;
        var expectedId = (await sut.Handle(new BuildProjectionPaths.Query(string.Empty, identity), CancellationToken.None)).Id;
        identity.Title += " [Preparing stream]";
        identity.EpisodeTitle += " [Downloading]";

        // Act
        var result = (await sut.Handle(new BuildProjectionPaths.Query(string.Empty, identity), CancellationToken.None)).Id;

        // Assert
        result.Should().Be(expectedId);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SanitizeMetadataIntoSafePathComponents(MediaIdentity identity, string directory, BuildProjectionPaths.Handler sut)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), directory);
        identity.Kind = MediaKind.Movie;
        identity.Title += " /../\\:\t?";

        // Act
        var path = (await sut.Handle(new BuildProjectionPaths.Query(root, identity), CancellationToken.None)).StreamPath;
        var relative = Path.GetRelativePath(root, path);
        var filename = Path.GetFileName(path);

        // Assert
        relative.Split(Path.DirectorySeparatorChar).Should().NotContain("..");
        filename.Should().EndWith(".strm").And.NotContain("?").And.NotContain(":").And.NotContain("\t");
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepEpisodeNumberingInDisplayPaths(MediaIdentity identity, string directory, BuildProjectionPaths.Handler sut)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), directory);
        identity.Kind = MediaKind.Episode;
        var numbering = $"S{identity.SeasonNumber:00}E{identity.EpisodeNumber:00}";

        // Act
        var path = (await sut.Handle(new BuildProjectionPaths.Query(root, identity), CancellationToken.None)).StreamPath;

        // Assert
        Path.GetFileName(path).Should().Contain(numbering);
        Path.GetFileName(Path.GetDirectoryName(path)).Should().Be($"Season {identity.SeasonNumber:00}");
    }

    [Theory, AutoMoqData]
    internal async Task Should_OmitUnknownYearsFromMoviePaths(MediaIdentity identity, string directory, BuildProjectionPaths.Handler sut)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), directory);
        identity.Kind = MediaKind.Movie;
        identity.Year = 0;

        // Act
        var path = (await sut.Handle(new BuildProjectionPaths.Query(root, identity), CancellationToken.None)).StreamPath;

        // Assert
        Path.GetFileName(path).Should().Be(identity.Title + ".strm");
    }
}
