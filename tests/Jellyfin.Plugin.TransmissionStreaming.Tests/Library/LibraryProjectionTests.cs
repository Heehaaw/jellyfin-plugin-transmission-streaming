namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public sealed class LibraryProjectionTests
{
    [Theory, AutoMoqData]
    internal async Task Should_LeaveTheLibraryAloneWhenTheProjectionIsNotIndexed(Projection source,
        [Frozen] Mock<ILibraryManager> library, [Frozen] Mock<IUserDataManager> userData, LibraryProjection sut)
    {
        // Arrange
        library.Setup(value => value.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([]);

        // Act
        await sut.ReconcileAsync(source, CancellationToken.None);

        // Assert
        library.Verify(value => value.DeleteItem(It.IsAny<BaseItem>(), It.IsAny<DeleteOptions>()), Times.Never);
        userData.Verify(value => value.GetUserDataBatch(It.IsAny<IReadOnlyList<BaseItem>>(), It.IsAny<User>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ShowDownloadStatusOnTheCanonicalItem(Projection source, Movie snapshot, Movie canonical, Folder parent, StreamingOptions options,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration, [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        source.CompletedPath = null;
        source.Identity.Kind = MediaKind.Movie;
        snapshot.Path = source.StreamPath;
        canonical.Id = snapshot.Id;
        canonical.Path = snapshot.Path;
        canonical.ParentId = parent.Id;
        Index(library, snapshot);
        library.Setup(value => value.GetItemById(snapshot.Id)).Returns(canonical);
        library.Setup(value => value.GetItemById(parent.Id)).Returns(parent);
        library.Setup(value => value.UpdateItemAsync(canonical, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        configuration.SetupGet(value => value.CurrentValue).Returns(options with { ShowDownloadStatus = true });
        var expected = $"{source.Identity.Title} [{source.Status}]";

        // Act
        await sut.ReconcileAsync(source, CancellationToken.None);

        // Assert
        canonical.Name.Should().Be(expected);
        snapshot.Name.Should().NotBe(expected);
        library.Verify(value => value.UpdateItemAsync(canonical, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RemoveTheTemporaryStatusAfterCompletion(Projection source, Movie projected, Folder parent, StreamingOptions options,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration, [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        projected.Path = source.StreamPath;
        projected.ParentId = parent.Id;
        Index(library, projected);
        library.Setup(value => value.GetItemById(parent.Id)).Returns(parent);
        library.Setup(value => value.GetItemList(It.Is<InternalItemsQuery>(query => query.Path == source.CompletedPath))).Returns([]);
        library.Setup(value => value.UpdateItemAsync(projected, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        configuration.SetupGet(value => value.CurrentValue).Returns(options with { ShowDownloadStatus = true });

        // Act
        await sut.ReconcileAsync(source, CancellationToken.None);

        // Assert
        projected.Name.Should().Be(source.Identity.Title);
        library.Verify(value => value.DeleteItem(It.IsAny<BaseItem>(), It.IsAny<DeleteOptions>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_UseEpisodeMetadataForItsDisplayTitle(Projection source, Movie projected, Folder parent, StreamingOptions options,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration, [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Episode;
        source.CompletedPath = null;
        projected.Path = source.StreamPath;
        projected.ParentId = parent.Id;
        Index(library, projected);
        library.Setup(value => value.GetItemById(parent.Id)).Returns(parent);
        library.Setup(value => value.UpdateItemAsync(projected, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        configuration.SetupGet(value => value.CurrentValue).Returns(options with { ShowDownloadStatus = false });

        // Act
        await sut.ReconcileAsync(source, CancellationToken.None);

        // Assert
        projected.Name.Should().Be(source.Identity.EpisodeTitle);
    }

    [Theory, AutoMoqData]
    internal async Task Should_DeferDuplicateRemovalWhileTheNativeCopyIsPlaying(Projection source, Movie projected, Movie native, SessionInfo session, StreamingOptions options,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration, [Frozen] Mock<ILibraryManager> library,
        [Frozen] Mock<ISessionManager> sessions, [Frozen] Mock<IUserManager> users, LibraryProjection sut)
    {
        // Arrange
        projected.Path = source.StreamPath;
        projected.Name = source.Identity.Title;
        source.Identity.Kind = MediaKind.Movie;
        source.CompletedPath = native.Path;
        Index(library, projected);
        Index(library, native);
        session.NowPlayingItem.Id = native.Id;
        sessions.SetupGet(value => value.Sessions).Returns([session]);
        configuration.SetupGet(value => value.CurrentValue).Returns(options with { ShowDownloadStatus = false });

        // Act
        await sut.ReconcileAsync(source, CancellationToken.None);

        // Assert
        users.Verify(value => value.GetUsers(), Times.Never);
        library.Verify(value => value.DeleteItem(It.IsAny<BaseItem>(), It.IsAny<DeleteOptions>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_MergeHydratedUserHistoryBeforeRemovingOnlyTheDuplicateEntry(Projection source, Movie projected, Movie native, User user,
        Dictionary<Guid, UserItemData> saved, UserItemData target, UpdateUserItemDataDto merged, StreamingOptions options,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> configuration, [Frozen] Mock<ILibraryManager> library,
        [Frozen] Mock<ISessionManager> sessions, [Frozen] Mock<IUserManager> users,
        [Frozen] Mock<IUserDataManager> userData, [Frozen] Mock<IWatchStateMerger> watchStates, LibraryProjection sut)
    {
        // Arrange
        projected.Path = source.StreamPath;
        projected.Name = source.Identity.Title;
        source.Identity.Kind = MediaKind.Movie;
        source.CompletedPath = native.Path;
        Index(library, projected);
        Index(library, native);
        sessions.SetupGet(value => value.Sessions).Returns([]);
        users.Setup(value => value.GetUsers()).Returns([user]);
        configuration.SetupGet(value => value.CurrentValue).Returns(options with { ShowDownloadStatus = false });
        var history = saved.Values.Single();
        saved.Clear();
        saved.Add(native.Id, history);
        saved.Add(projected.Id, target);
        userData.Setup(value => value.GetUserDataBatch(It.IsAny<IReadOnlyList<BaseItem>>(), user)).Returns(saved);
        watchStates.Setup(value => value.Merge(history, target)).Returns(merged);
        var operations = new List<string>();
        var mergeOperation = "merge";
        var deleteOperation = "delete";
        userData.Setup(value => value.SaveUserData(user, projected, merged, UserDataSaveReason.Import)).Callback(() => operations.Add(mergeOperation));
        library.Setup(value => value.DeleteItem(native, It.Is<DeleteOptions>(options => !options.DeleteFileLocation))).Callback(() => operations.Add(deleteOperation));

        // Act
        await sut.ReconcileAsync(source, CancellationToken.None);

        // Assert
        userData.Verify(value => value.GetUserDataBatch(It.Is<IReadOnlyList<BaseItem>>(items =>
            items.Count == 2 && items[0] == native && items[1] == projected), user), Times.Once);
        operations.Should().Equal(mergeOperation, deleteOperation);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SkipUnrelatedLibrariesDuringRefresh(VirtualFolderInfo source, string unrelatedDirectory,
        [Frozen] Mock<IProjectionFiles> files, [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        library.Setup(value => value.GetVirtualFolders(false)).Returns([source]);
        files.SetupGet(value => value.Root).Returns(Path.Combine(Path.GetTempPath(), unrelatedDirectory));

        // Act
        await sut.RefreshAsync(CancellationToken.None);

        // Assert
        library.Verify(value => value.GetItemById(It.IsAny<Guid>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal void Should_ReportTheCanonicalProjectionAsIndexed(Projection source, Movie projected,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        projected.Path = source.StreamPath;
        Index(library, projected);

        // Act
        var result = sut.IsIndexed(source);

        // Assert
        result.Should().BeTrue();
    }

    private static void Index(Mock<ILibraryManager> library, BaseItem item)
    {
        library.Setup(value => value.GetItemList(It.Is<InternalItemsQuery>(query => query.Path == item.Path))).Returns([item]);
        library.Setup(value => value.GetItemById(item.Id)).Returns(item);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<Movie>(composer =>
            composer.OmitAutoProperties().With(value => value.Id).With(value => value.ParentId).With(value => value.Path).With(value => value.Name));
        fixture.Customize<Folder>(composer => composer.OmitAutoProperties().With(value => value.Id).With(value => value.Path));
        fixture.Customize<BaseItemDto>(composer => composer.OmitAutoProperties().With(value => value.Id));
        fixture.Customize<SessionInfo>(composer => composer.OmitAutoProperties().With(value => value.NowPlayingItem));
        fixture.Customize<User>(composer => composer.OmitAutoProperties());
        fixture.Customize<UserItemData>(composer => composer.With(value => value.Rating, () => fixture.Create<int>() % 11d).Without(value => value.Likes));
        return fixture;
    });
}
