namespace TransmissionStreaming.Infrastructure.Tests.Filesystem;

using FluentAssertions;
using global::TransmissionStreaming.Infrastructure.Filesystem;
using Xunit;

public sealed class PhysicalMediaFilesTests
{
    [Theory, AutoMoqData]
    internal void Should_AcceptOnlyAnExistingFileWithTheExactLength(byte[] source, string name, TemporaryDirectory directory, PhysicalMediaFiles sut)
    {
        // Arrange
        using var root = directory;
        var path = Path.Combine(root.Path, name);
        File.WriteAllBytes(path, source);

        // Act
        var exact = sut.HasLength(path, source.Length);
        var changedRelease = sut.HasLength(path, source.Length + 1);
        File.Delete(path);
        var removed = sut.HasLength(path, source.Length);
        var missingDirectory = sut.HasLength(Path.Combine(path, name), source.Length);

        // Assert
        exact.Should().BeTrue();
        changedRelease.Should().BeFalse();
        removed.Should().BeFalse();
        missingDirectory.Should().BeFalse();
    }
}
