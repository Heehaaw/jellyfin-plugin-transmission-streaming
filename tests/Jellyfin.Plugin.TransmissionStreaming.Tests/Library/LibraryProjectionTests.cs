namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Projection;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

public sealed class LibraryProjectionTests
{
    [Theory, AutoMoqData]
    internal void Should_ReadCanonicalFactsAndCurrentPlayback(Movie snapshot, Movie canonical, SessionInfo session,
        [Frozen] Mock<ILibraryManager> library, [Frozen] Mock<ISessionManager> sessions, LibraryProjection sut)
    {
        // Arrange
        canonical.Id = snapshot.Id;
        canonical.Path = snapshot.Path;
        Index(library, snapshot);
        library.Setup(value => value.GetItemById(snapshot.Id)).Returns(canonical);
        session.NowPlayingItem.Id = canonical.Id;
        sessions.SetupGet(value => value.Sessions).Returns([session]);

        // Act
        var result = sut.Find(snapshot.Path);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(canonical.Id.ToString("N"));
        result.Name.Should().Be(canonical.Name);
        result.Overview.Should().Be(canonical.Overview);
        result.IsPlaying.Should().BeTrue();
        library.Verify(value => value.GetItemList(It.Is<InternalItemsQuery>(query => query.Path == snapshot.Path && query.Recursive)), Times.Once);
    }

    [Theory, AutoMoqData]
    internal void Should_UseTheExactPathSnapshotWhenTheCanonicalItemIsUnavailable(Movie source,
        [Frozen] Mock<ILibraryManager> library, [Frozen] Mock<ISessionManager> sessions, LibraryProjection sut)
    {
        // Arrange
        Index(library, source);
        library.Setup(value => value.GetItemById(source.Id)).Returns((BaseItem?)null);
        sessions.SetupGet(value => value.Sessions).Returns([]);

        // Act
        var result = sut.Find(source.Path);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(source.Id.ToString("N"));
        result.Name.Should().Be(source.Name);
        result.IsPlaying.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectResultsForAnotherPath(Movie source, string requestedPath,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        library.Setup(value => value.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([source]);

        // Act
        var result = sut.Find(requestedPath);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_ReturnNoFactsWhenThePathIsNotIndexed(string path,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        library.Setup(value => value.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([]);

        // Act
        var result = sut.Find(path);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_WriteMetadataOnTheCanonicalItem(Movie source, Folder parent, LibraryItem metadata,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        source.ParentId = parent.Id;
        Index(library, source);
        library.Setup(value => value.GetItemById(parent.Id)).Returns(parent);
        library.Setup(value => value.UpdateItemAsync(source, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await sut.UpdateMetadataAsync(source.Id.ToString("N"), metadata.Name, metadata.Overview, CancellationToken.None);

        // Assert
        source.Name.Should().Be(metadata.Name);
        source.Overview.Should().Be(metadata.Overview);
        library.Verify(value => value.UpdateItemAsync(source, parent, ItemUpdateType.MetadataEdit, CancellationToken.None), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseMetadataWritesWhenTheParentIsUnavailable(Movie source,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        Index(library, source);
        library.Setup(value => value.GetItemById(source.ParentId)).Returns((BaseItem?)null);

        // Act
        var action = () => sut.UpdateMetadataAsync(source.Id.ToString("N"), source.Name, source.Overview, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        library.Verify(value => value.UpdateItemAsync(It.IsAny<BaseItem>(), It.IsAny<BaseItem>(), It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PropagateMetadataWriteFailures(Movie source, Folder parent, IOException failure,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        source.ParentId = parent.Id;
        Index(library, source);
        library.Setup(value => value.GetItemById(parent.Id)).Returns(parent);
        library.Setup(value => value.UpdateItemAsync(source, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        // Act
        var action = () => sut.UpdateMetadataAsync(source.Id.ToString("N"), source.Name, source.Overview, CancellationToken.None);

        // Assert
        (await action.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(failure);
    }

    [Theory, AutoMoqData]
    internal void Should_SnapshotHydratedSourceAndTargetHistory(Movie source, Movie target, User user,
        UserItemData sourceState, UserItemData targetState, Dictionary<Guid, UserItemData> saved,
        [Frozen] Mock<ILibraryManager> library, [Frozen] Mock<IUserManager> users, [Frozen] Mock<IUserDataManager> userData, LibraryProjection sut)
    {
        // Arrange
        Index(library, source);
        Index(library, target);
        users.Setup(value => value.GetUsers()).Returns([user]);
        saved.Clear();
        saved.Add(source.Id, sourceState);
        saved.Add(target.Id, targetState);
        userData.Setup(value => value.GetUserDataBatch(It.IsAny<IReadOnlyList<BaseItem>>(), user)).Returns(saved);

        // Act
        var result = sut.GetUserStates(source.Id.ToString("N"), target.Id.ToString("N"));

        // Assert
        var state = result.Should().ContainSingle().Subject;
        state.UserId.Should().Be(user.Id.ToString("N"));
        AssertWatchState(state.Source!, sourceState);
        AssertWatchState(state.Target!, targetState);
        userData.Verify(value => value.GetUserDataBatch(It.Is<IReadOnlyList<BaseItem>>(items =>
            items.Count == 2 && items[0] == source && items[1] == target), user), Times.Once);
    }

    [Theory, AutoMoqData]
    internal void Should_KeepAbsentUserRowsAsAbsent(Movie source, Movie target, User user, Dictionary<Guid, UserItemData> saved,
        [Frozen] Mock<ILibraryManager> library, [Frozen] Mock<IUserManager> users, [Frozen] Mock<IUserDataManager> userData, LibraryProjection sut)
    {
        // Arrange
        Index(library, source);
        Index(library, target);
        users.Setup(value => value.GetUsers()).Returns([user]);
        saved.Clear();
        userData.Setup(value => value.GetUserDataBatch(It.IsAny<IReadOnlyList<BaseItem>>(), user)).Returns(saved);

        // Act
        var result = sut.GetUserStates(source.Id.ToString("N"), target.Id.ToString("N"));

        // Assert
        var state = result.Should().ContainSingle().Subject;
        state.Source.Should().BeNull();
        state.Target.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_SaveAllWatchFieldsToTheRequestedUserAndItem(Movie target, User user, LibraryWatchState state,
        [Frozen] Mock<ILibraryManager> library, [Frozen] Mock<IUserManager> users, [Frozen] Mock<IUserDataManager> userData, LibraryProjection sut)
    {
        // Arrange
        Index(library, target);
        users.Setup(value => value.GetUsers()).Returns([user]);
        UpdateUserItemDataDto? captured = null;
        userData.Setup(value => value.SaveUserData(user, target, It.IsAny<UpdateUserItemDataDto>(), UserDataSaveReason.Import))
            .Callback<User, BaseItem, UpdateUserItemDataDto, UserDataSaveReason>((_, _, value, _) => captured = value);

        // Act
        sut.SaveUserState(user.Id.ToString("N"), target.Id.ToString("N"), state);

        // Assert
        captured.Should().NotBeNull();
        captured.Should().BeEquivalentTo(state, options => options.ExcludingMissingMembers());
        userData.Verify(value => value.SaveUserData(user, target, It.IsAny<UpdateUserItemDataDto>(), UserDataSaveReason.Import), Times.Once);
    }

    [Theory, AutoMoqData]
    internal void Should_RefuseHistoryWritesForAnUnavailableUser(User source, Movie target, LibraryWatchState state,
        [Frozen] Mock<IUserManager> users, [Frozen] Mock<IUserDataManager> userData, LibraryProjection sut)
    {
        // Arrange
        users.Setup(value => value.GetUsers()).Returns([]);

        // Act
        var action = () => sut.SaveUserState(source.Id.ToString("N"), target.Id.ToString("N"), state);

        // Assert
        action.Should().Throw<InvalidOperationException>();
        userData.Verify(value => value.SaveUserData(It.IsAny<User>(), It.IsAny<BaseItem>(), It.IsAny<UpdateUserItemDataDto>(), It.IsAny<UserDataSaveReason>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal void Should_RemoveOnlyTheHostDatabaseEntry(Movie source,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        Index(library, source);

        // Act
        sut.Remove(source.Id.ToString("N"));

        // Assert
        library.Verify(value => value.DeleteItem(source, It.Is<DeleteOptions>(options => !options.DeleteFileLocation)), Times.Once);
    }

    [Theory, AutoMoqData]
    internal void Should_AllowRemovalOfAnAlreadyMissingHostEntry(Guid itemId,
        [Frozen] Mock<ILibraryManager> library, LibraryProjection sut)
    {
        // Arrange
        library.Setup(value => value.GetItemById(itemId)).Returns((BaseItem?)null);

        // Act
        sut.Remove(itemId.ToString("N"));

        // Assert
        library.Verify(value => value.DeleteItem(It.IsAny<BaseItem>(), It.IsAny<DeleteOptions>()), Times.Never);
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

    private static void AssertWatchState(LibraryWatchState actual, UserItemData expected)
    {
        actual.Should().NotBeNull();
        actual.PlaybackPositionTicks.Should().Be(expected.PlaybackPositionTicks);
        actual.Played.Should().Be(expected.Played);
        actual.LastPlayedDate.Should().Be(expected.LastPlayedDate);
        actual.PlayCount.Should().Be(expected.PlayCount);
        actual.IsFavorite.Should().Be(expected.IsFavorite);
        actual.Rating.Should().Be(expected.Rating);
        actual.Likes.Should().Be(expected.Likes);
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
        fixture.Customize<Movie>(composer => composer.OmitAutoProperties().With(value => value.Id).With(value => value.ParentId)
            .With(value => value.Path).With(value => value.Name).With(value => value.Overview));
        fixture.Customize<Folder>(composer => composer.OmitAutoProperties().With(value => value.Id).With(value => value.Path));
        fixture.Customize<BaseItemDto>(composer => composer.OmitAutoProperties().With(value => value.Id));
        fixture.Customize<SessionInfo>(composer => composer.OmitAutoProperties().With(value => value.NowPlayingItem));
        fixture.Customize<User>(composer => composer.OmitAutoProperties());
        fixture.Customize<UserItemData>(composer => composer.With(value => value.Rating, () => fixture.Create<int>() % 11d).Without(value => value.Likes));
        return fixture;
    });
}
