namespace TransmissionStreaming.Logic.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Time.Testing;
using Moq;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Logic.Discovery;
using TransmissionStreaming.Logic.Imports;
using TransmissionStreaming.Logic.Library;
using Xunit;

public sealed class HandoffCompletedProjectionTests
{
    [Theory, AutoMoqData]
    internal async Task Should_RetainSeedingAndResetPendingIntent(HandoffCompletedProjection.Command source,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        source.Entry.HandoffPending = true;
        var command = source with { TorrentPresent = true };

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeFalse();
        source.Entry.HandoffPending.Should().BeFalse();
        library.Verify(value => value.Find(It.IsAny<string>()), Times.Never);
        store.Verify(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_WaitForAValidatedImportedFile(HandoffCompletedProjection.Command source,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeFalse();
        library.Verify(value => value.Find(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_DeferWhileProxyBytesAreBeingRead(HandoffCompletedProjection.Command source,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IProjectionLibrary> library, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(1);

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeFalse();
        library.Verify(value => value.Find(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PublishIndexingIntentAndWaitForTheNativeItem(HandoffCompletedProjection.Command source, LibraryItem projected,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        source.Entry.HandoffPending = false;
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source.Entry.CompletedPath));
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected with { IsPlaying = false });
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns((LibraryItem?)null);
        bool? observedIntent = null;
        library.Setup(value => value.IndexImportedAsync(source.Entry.CompletedPath!, It.IsAny<CancellationToken>()))
            .Callback(() => observedIntent = source.Entry.HandoffPending).Returns(Task.CompletedTask);

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeFalse();
        observedIntent.Should().BeTrue();
        store.Verify(value => value.Save(source.Entry), Times.Once);
        store.Verify(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_WaitForPlaybackOnEitherHostEntry(HandoffCompletedProjection.Command source, LibraryItem projected, LibraryItem native,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source.Entry.CompletedPath));
        var results = new List<bool>();

        // Act
        foreach (var projectedPlaying in new[] { true, false })
        {
            library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected with { IsPlaying = projectedPlaying });
            library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(native with { IsPlaying = !projectedPlaying });
            results.Add(await sut.Handle(command, CancellationToken.None));
        }

        // Assert
        results.Should().OnlyContain(completed => !completed);
        store.Verify(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()), Times.Never);
        library.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_TransferHistoryBeforeRetiringTheProxy(HandoffCompletedProjection.Command source, LibraryItem projected, LibraryItem native,
        [Frozen] FakeTimeProvider clock, [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionOverview> overviews,
        [Frozen] Mock<IProjectionFiles> files, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        source.Entry.Identity.Kind = MediaKind.Movie;
        source.Entry.HandoffPending = false;
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source.Entry.CompletedPath));
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected with { IsPlaying = false });
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(native with { IsPlaying = false });
        overviews.Setup(value => value.Format(It.IsAny<string?>(), It.IsAny<string?>())).Returns(native.Overview);
        var operations = new List<string>();
        var pending = "pending"; var transfer = "transfer"; var remove = "remove"; var cleanup = "files"; var archive = "archive";
        store.Setup(value => value.Save(source.Entry)).Callback(() => operations.Add(pending));
        mediator.Setup(value => value.Send(It.Is<TransferUserHistory.Command>(request => request.SourceItemId == projected.Id && request.TargetItemId == native.Id), It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add(transfer)).Returns(ValueTask.FromResult(Unit.Value));
        library.Setup(value => value.Remove(projected.Id)).Callback(() => operations.Add(remove));
        files.Setup(value => value.RemoveFiles(source.Entry)).Callback(() => operations.Add(cleanup));
        CompletedProjection? recorded = null;
        var retention = 0;
        store.Setup(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()))
            .Callback<CompletedProjection, int>((value, count) => { recorded = value; retention = count; operations.Add(archive); });
        const int expectedRetention = 20;

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeTrue();
        operations.Should().Equal(pending, transfer, remove, cleanup, archive);
        recorded!.Id.Should().Be(source.Entry.Id);
        recorded.Title.Should().Be(source.Entry.Identity.Title);
        recorded.NativeItemId.Should().Be(native.Id);
        recorded.CompletedAt.Should().Be(clock.GetUtcNow());
        retention.Should().Be(expectedRetention);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepRetirementRetryableWhenFileCleanupFails(HandoffCompletedProjection.Command source, LibraryItem native,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionOverview> overviews,
        [Frozen] Mock<IProjectionFiles> files, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        source.Entry.HandoffPending = true;
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source.Entry.CompletedPath));
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns((LibraryItem?)null);
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(native with { IsPlaying = false });
        overviews.Setup(value => value.Format(It.IsAny<string?>(), It.IsAny<string?>())).Returns(native.Overview);
        files.Setup(value => value.RemoveFiles(source.Entry)).Throws<IOException>();

        // Act
        var action = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<IOException>();
        store.Verify(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()), Times.Never);
        source.Entry.HandoffPending.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal async Task Should_DeferWhenPlaybackStartsDuringIndexing(HandoffCompletedProjection.Command source, LibraryItem projected, LibraryItem native,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        var command = source with { TorrentPresent = false };
        activity.SetupSequence(value => value.Count(source.Entry.Id)).Returns(0).Returns(1);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source.Entry.CompletedPath));
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected with { IsPlaying = false });
        library.SetupSequence(value => value.Find(source.Entry.CompletedPath!)).Returns((LibraryItem?)null).Returns(native with { IsPlaying = false });

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeFalse();
        library.Verify(value => value.IndexImportedAsync(source.Entry.CompletedPath!, It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(value => value.Send(It.IsAny<TransferUserHistory.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RetainTheSourceWhenPlaybackStartsDuringMetadataWrites(HandoffCompletedProjection.Command source, LibraryItem projected, LibraryItem native,
        [Frozen] Mock<IStreamActivity> activity, [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IProjectionLibrary> library, [Frozen] Mock<IProjectionOverview> overviews,
        [Frozen] Mock<IProjectionFiles> files, [Frozen] Mock<IProjectionStore> store, HandoffCompletedProjection.Handler sut)
    {
        // Arrange
        var command = source with { TorrentPresent = false };
        activity.Setup(value => value.Count(source.Entry.Id)).Returns(0);
        mediator.Setup(value => value.Send(It.IsAny<ResolveImportedFile.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source.Entry.CompletedPath));
        library.Setup(value => value.Find(source.Entry.StreamPath)).Returns(projected with { IsPlaying = false });
        library.Setup(value => value.Find(source.Entry.CompletedPath!)).Returns(native with { IsPlaying = false });
        overviews.Setup(value => value.Format(It.IsAny<string?>(), It.IsAny<string?>())).Returns(native.Overview + " updated");
        library.Setup(value => value.UpdateMetadataAsync(native.Id, native.Name, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => activity.Setup(value => value.Count(source.Entry.Id)).Returns(1)).Returns(Task.CompletedTask);

        // Act
        var completed = await sut.Handle(command, CancellationToken.None);

        // Assert
        completed.Should().BeFalse();
        library.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
        files.Verify(value => value.RemoveFiles(It.IsAny<Projection>()), Times.Never);
        store.Verify(value => value.Complete(It.IsAny<CompletedProjection>(), It.IsAny<int>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<FakeTimeProvider>(composer => composer.OmitAutoProperties());
        fixture.Register<TimeProvider>(() => fixture.Create<FakeTimeProvider>());
        return fixture;
    });
}
