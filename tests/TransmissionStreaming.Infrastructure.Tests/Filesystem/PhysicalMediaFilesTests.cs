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

    [Theory, AutoMoqData]
    internal async Task Should_ReadTheRequestedBytesAtAFileRelativeOffset(byte[] source, string name, TemporaryDirectory directory, PhysicalMediaFiles sut)
    {
        // Arrange
        using var root = directory;
        var path = Path.Combine(root.Path, name);
        var payload = source.Concat(source).ToArray();
        var offset = source.Length;
        var buffer = new byte[source.Length];
        await File.WriteAllBytesAsync(path, payload);

        // Act
        var count = await sut.ReadAsync(path, buffer, offset, CancellationToken.None);

        // Assert
        count.Should().Be(source.Length);
        buffer.Should().Equal(source);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReturnNoBytesWhenTheFileOrDirectoryIsMissing(string name, TemporaryDirectory directory, PhysicalMediaFiles sut)
    {
        // Arrange
        using var root = directory;
        var missingFile = Path.Combine(root.Path, name);
        var missingDirectory = Path.Combine(missingFile, name);
        var buffer = new byte[4096];

        // Act
        var absentFile = await sut.ReadAsync(missingFile, buffer, 0, CancellationToken.None);
        var absentDirectory = await sut.ReadAsync(missingDirectory, buffer, 0, CancellationToken.None);

        // Assert
        absentFile.Should().Be(0);
        absentDirectory.Should().Be(0);
        buffer.Should().OnlyContain(value => value == 0);
    }

    [Theory, AutoMoqData]
    internal async Task Should_CancelBeforeOpeningTheBackingFile(string path, byte[] buffer, PhysicalMediaFiles sut)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var reading = () => sut.ReadAsync(path, buffer, 0, cancellation.Token).AsTask();

        // Assert
        await reading.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RecognizeAnExistingFileWithoutTreatingDirectoriesAsFiles(byte[] source, string name, TemporaryDirectory directory, PhysicalMediaFiles sut)
    {
        // Arrange
        using var root = directory;
        var path = Path.Combine(root.Path, name);
        File.WriteAllBytes(path, source);

        // Act
        var exists = sut.Exists(path);
        var directoryExistsAsFile = sut.Exists(root.Path);
        File.Delete(path);
        var removed = sut.Exists(path);

        // Assert
        exists.Should().BeTrue();
        directoryExistsAsFile.Should().BeFalse();
        removed.Should().BeFalse();
    }
}
