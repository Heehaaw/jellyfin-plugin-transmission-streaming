namespace TransmissionStreaming.Logic.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Moq;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Library;
using Xunit;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;

public sealed class ShouldIgnoreImportedFileTests
{
    [Theory, AutoMoqData]
    internal async Task Should_SuppressAnUnindexedNativeDuplicate(ShouldIgnoreImportedFile.Query source, Projection entry, LibraryItem projected, StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IMediaFiles> files, [Frozen] Mock<IProjectionLibrary> library, ShouldIgnoreImportedFile.Handler sut)
    {
        // Arrange
        var query = source with { Path = entry.CompletedPath!, IsDirectory = false };
        entry.HandoffPending = false;
        options.SetupGet(value => value.CurrentValue).Returns(configuration with { Enabled = true });
        store.Setup(value => value.All()).Returns([entry]);
        files.Setup(value => value.Exists(entry.StreamPath)).Returns(true);
        library.Setup(value => value.Find(entry.StreamPath)).Returns(projected);
        library.Setup(value => value.Find(query.Path)).Returns((LibraryItem?)null);

        // Act
        var ignored = await sut.Handle(query, CancellationToken.None);

        // Assert
        ignored.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowIndexingDuringHandoff(ShouldIgnoreImportedFile.Query source, Projection entry, StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IProjectionLibrary> library, ShouldIgnoreImportedFile.Handler sut)
    {
        // Arrange
        var query = source with { Path = entry.CompletedPath!, IsDirectory = false };
        entry.HandoffPending = true;
        options.SetupGet(value => value.CurrentValue).Returns(configuration with { Enabled = true });
        store.Setup(value => value.All()).Returns([entry]);

        // Act
        var ignored = await sut.Handle(query, CancellationToken.None);

        // Assert
        ignored.Should().BeFalse();
        library.Verify(value => value.Find(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepAnIndexedNativeItemVisibleUntilHistoryTransfer(ShouldIgnoreImportedFile.Query source, Projection entry, LibraryItem native, StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IMediaFiles> files, [Frozen] Mock<IProjectionLibrary> library, ShouldIgnoreImportedFile.Handler sut)
    {
        // Arrange
        var query = source with { Path = entry.CompletedPath!, IsDirectory = false };
        entry.HandoffPending = false;
        options.SetupGet(value => value.CurrentValue).Returns(configuration with { Enabled = true });
        store.Setup(value => value.All()).Returns([entry]);
        files.Setup(value => value.Exists(entry.StreamPath)).Returns(true);
        library.Setup(value => value.Find(It.IsAny<string>())).Returns(native);

        // Act
        var ignored = await sut.Handle(query, CancellationToken.None);

        // Assert
        ignored.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowDirectoriesAndDisabledProjections(ShouldIgnoreImportedFile.Query source, StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store, ShouldIgnoreImportedFile.Handler sut)
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(configuration with { Enabled = true });
        var directory = source with { IsDirectory = true };

        // Act
        var directoryIgnored = await sut.Handle(directory, CancellationToken.None);
        options.SetupGet(value => value.CurrentValue).Returns(configuration with { Enabled = false });
        var disabledIgnored = await sut.Handle(source with { IsDirectory = false }, CancellationToken.None);

        // Assert
        directoryIgnored.Should().BeFalse();
        disabledIgnored.Should().BeFalse();
        store.Verify(value => value.All(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowNativeMediaWithoutAUsableProjection(ShouldIgnoreImportedFile.Query source, Projection entry, StreamingOptions configuration,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, [Frozen] Mock<IProjectionStore> store,
        [Frozen] Mock<IMediaFiles> files, [Frozen] Mock<IProjectionLibrary> library, ShouldIgnoreImportedFile.Handler sut)
    {
        // Arrange
        var query = source with { Path = entry.CompletedPath!, IsDirectory = false };
        entry.HandoffPending = false;
        options.SetupGet(value => value.CurrentValue).Returns(configuration with { Enabled = true });
        store.Setup(value => value.All()).Returns([entry]);
        library.Setup(value => value.Find(entry.StreamPath)).Returns((LibraryItem?)null);
        files.Setup(value => value.Exists(entry.StreamPath)).Returns(true);

        // Act
        var ignored = await sut.Handle(query, CancellationToken.None);

        // Assert
        ignored.Should().BeFalse();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 2 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
