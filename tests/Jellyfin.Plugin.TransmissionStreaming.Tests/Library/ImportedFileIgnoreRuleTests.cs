namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using MediaBrowser.Model.IO;
using Moq;
using Xunit;

public sealed class ImportedFileIgnoreRuleTests
{
    [Theory, AutoMoqData]
    internal void Should_KeepNativeFilesVisibleWhenDisabled(StreamingOptions options, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store, ImportedFileIgnoreRule sut)
    {
        // Arrange
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = false });

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeFalse();
        store.Verify(value => value.All(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveDirectoriesToJellyfin(StreamingOptions options, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store, ImportedFileIgnoreRule sut)
    {
        // Arrange
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = true });
        source.IsDirectory = true;

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeFalse();
        store.Verify(value => value.All(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal void Should_KeepUnmanagedFilesVisible(StreamingOptions options, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store, ImportedFileIgnoreRule sut)
    {
        // Arrange
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = true });
        source.IsDirectory = false;
        store.Setup(value => value.All()).Returns([]);

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_KeepTheNativeFileWhenItsStreamFileIsMissing(StreamingOptions options, Projection entry, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store, ImportedFileIgnoreRule sut)
    {
        // Arrange
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = true });
        source.IsDirectory = false;
        entry.CompletedPath = source.FullName;
        store.Setup(value => value.All()).Returns([entry]);

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_WaitUntilTheProjectedEntryIsIndexed(StreamingOptions options, Projection entry, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionIndex> index, ImportedFileIgnoreRule sut)
    {
        // Arrange
        using var stream = CreatedStreamFile.At(entry.StreamPath);
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = true });
        source.IsDirectory = false;
        entry.CompletedPath = source.FullName;
        store.Setup(value => value.All()).Returns([entry]);
        index.Setup(value => value.IsIndexed(entry)).Returns(false);

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_KeepAnIndexedNativeEntryUntilItsHistoryIsMerged(StreamingOptions options, Projection entry, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionIndex> index, ImportedFileIgnoreRule sut)
    {
        // Arrange
        using var stream = CreatedStreamFile.At(entry.StreamPath);
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = true });
        source.IsDirectory = false;
        entry.CompletedPath = source.FullName;
        store.Setup(value => value.All()).Returns([entry]);
        index.Setup(value => value.IsIndexed(entry)).Returns(true);
        index.Setup(value => value.IsPathIndexed(source.FullName)).Returns(true);

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_HideTheNativeFileAfterSafeReconciliation(StreamingOptions options, Projection entry, FileSystemMetadata source,
        [Frozen] Mock<IStreamingOptionsProvider> configuration, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionIndex> index, ImportedFileIgnoreRule sut)
    {
        // Arrange
        using var stream = CreatedStreamFile.At(entry.StreamPath);
        configuration.Setup(value => value.Get()).Returns(options with { Enabled = true });
        source.IsDirectory = false;
        entry.CompletedPath = source.FullName;
        store.Setup(value => value.All()).Returns([entry]);
        index.Setup(value => value.IsIndexed(entry)).Returns(true);
        index.Setup(value => value.IsPathIndexed(source.FullName)).Returns(false);

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().BeTrue();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<Projection>(composer => composer.With(value => value.StreamPath, () => Path.Combine(Path.GetTempPath(), fixture.Create<string>())));
        return fixture;
    });

    private sealed class CreatedStreamFile(string path) : IDisposable
    {
        public static CreatedStreamFile At(string path)
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            return new CreatedStreamFile(path);
        }

        public void Dispose()
        {
            File.Delete(path);
        }
    }
}
