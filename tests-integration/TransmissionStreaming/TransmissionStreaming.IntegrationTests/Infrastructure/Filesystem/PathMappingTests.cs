namespace TransmissionStreaming.IntegrationTests.Infrastructure.Filesystem;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using FluentAssertions;
using global::TransmissionStreaming.Infrastructure.Filesystem;
using Xunit;

public sealed class PathMappingTests
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
            fixture.Register(() => new PathMapping("/data", Path.GetTempPath()));
            return fixture;
        }
    );

    [Theory, AutoMoqData]
    internal void Should_MapOnlyWholeRootBoundaries(string fileName, PathMapping sut)
    {
        // Arrange
        var path = sut.RemoteRoot + "-other/" + fileName;

        // Act
        var result = sut.Map(path);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectParentTraversal(string fileName, PathMapping sut)
    {
        // Arrange
        var path = sut.RemoteRoot + "/../" + fileName;

        // Act
        var result = sut.Map(path);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_MapApprovedMedia(string fileName, PathMapping sut)
    {
        // Arrange
        var path = sut.RemoteRoot + "/" + fileName;
        var expectedPath = Path.Combine(sut.LocalRoot, fileName);

        // Act
        var result = sut.Map(path);

        // Assert
        result.Should().Be(expectedPath);
    }
}
