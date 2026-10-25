namespace TransmissionStreaming.IntegrationTests.Infrastructure.Filesystem;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.Infrastructure.Filesystem;
using global::TransmissionStreaming.TestSupport.Testing;
using Xunit;

public sealed class PhysicalMediaPathMapperTests
{
    [Theory, AutoMoqData]
    internal void Should_RejectSymbolicLinkFiles(string name, TemporaryDirectory directory, PhysicalMediaPathMapper sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";
        var link = Path.Combine(root.Path, name);
        File.CreateSymbolicLink(link, Path.Combine(Path.GetTempPath(), name));

        // Act
        var result = sut.Map(remoteRoot + "/" + name, remoteRoot, root.Path);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectSymbolicLinkDirectories(string name, TemporaryDirectory directory, PhysicalMediaPathMapper sut)
    {
        // Arrange
        using var root = directory;
        var remoteRoot = "/data";
        var link = Path.Combine(root.Path, name);
        Directory.CreateSymbolicLink(link, Path.GetTempPath());

        // Act
        var result = sut.Map(remoteRoot + "/" + name + "/movie.mkv", remoteRoot, root.Path);

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
