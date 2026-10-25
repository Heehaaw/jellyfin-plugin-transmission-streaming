namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Projection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

/// <summary>Maps Jellyfin indexing, metadata, sessions, and user-data APIs onto host-neutral contracts.</summary>
internal sealed class LibraryProjection(
    IProjectionFiles files,
    ILibraryManager library,
    IUserManager users,
    IUserDataManager userData,
    ISessionManager sessions) : IProjectionLibrary
{
    public Task RefreshAsync(CancellationToken cancellationToken)
    {
        return RefreshContainingAsync(files.Root, true, cancellationToken);
    }

    public Task IndexImportedAsync(string path, CancellationToken cancellationToken)
    {
        return RefreshContainingAsync(path, false, cancellationToken);
    }

    public LibraryItem? Find(string path)
    {
        var item = FindNative(path);
        return item is null ? null : new LibraryItem(item.Id.ToString("N"), item.Name, item.Overview,
            sessions.Sessions.Any(session => session.NowPlayingItem?.Id == item.Id));
    }

    public async Task UpdateMetadataAsync(string itemId, string name, string? overview, CancellationToken cancellationToken)
    {
        var item = RequiredItem(itemId);
        var parent = library.GetItemById(item.ParentId)
            ?? throw new InvalidOperationException("The library item's parent is unavailable.");
        item.Name = name;
        item.Overview = overview;
        await library.UpdateItemAsync(item, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<LibraryUserState> GetUserStates(string sourceItemId, string targetItemId)
    {
        var source = RequiredItem(sourceItemId);
        var target = RequiredItem(targetItemId);
        var result = new List<LibraryUserState>();
        foreach (var user in users.GetUsers())
        {
            // The batch API hydrates saved rows; single-item access can return an empty default.
            var saved = userData.GetUserDataBatch([source, target], user);
            saved.TryGetValue(source.Id, out var sourceState);
            saved.TryGetValue(target.Id, out var targetState);
            result.Add(new LibraryUserState(user.Id.ToString("N"), Snapshot(sourceState), Snapshot(targetState)));
        }
        return result;
    }

    public void SaveUserState(string userId, string itemId, LibraryWatchState state)
    {
        var targetUserId = Guid.Parse(userId);
        var user = users.GetUsers().FirstOrDefault(candidate => candidate.Id == targetUserId)
            ?? throw new InvalidOperationException("The user is unavailable for history transfer.");
        userData.SaveUserData(user, RequiredItem(itemId), new UpdateUserItemDataDto
        {
            PlaybackPositionTicks = state.PlaybackPositionTicks,
            Played = state.Played,
            LastPlayedDate = state.LastPlayedDate,
            PlayCount = state.PlayCount,
            IsFavorite = state.IsFavorite,
            Rating = state.Rating,
            Likes = state.Likes
        }, UserDataSaveReason.Import);
    }

    public void Remove(string itemId)
    {
        if (library.GetItemById(Guid.Parse(itemId)) is { } item)
        {
            library.DeleteItem(item, new DeleteOptions { DeleteFileLocation = false });
        }
    }

    private async Task RefreshContainingAsync(string path, bool includeDescendants, CancellationToken cancellationToken)
    {
        foreach (var folder in library.GetVirtualFolders(false))
        {
            if (!folder.Locations.Any(location => IsWithin(location, path) || includeDescendants && IsWithin(path, location)))
            {
                continue;
            }
            if (Guid.TryParse(folder.ItemId, out var id) && library.GetItemById(id) is Folder item)
            {
                await item.ValidateChildren(new Progress<double>(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private BaseItem RequiredItem(string id)
    {
        return library.GetItemById(Guid.Parse(id)) ?? throw new InvalidOperationException("The library item is unavailable.");
    }

    private BaseItem? FindNative(string path)
    {
        var match = library.GetItemList(new InternalItemsQuery { Path = path, Recursive = true })
            .FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.Ordinal));
        return match is null ? null : library.GetItemById(match.Id) ?? match;
    }

    private static LibraryWatchState? Snapshot(UserItemData? state)
    {
        return state is null ? null : new LibraryWatchState
        {
            PlaybackPositionTicks = state.PlaybackPositionTicks,
            Played = state.Played,
            LastPlayedDate = state.LastPlayedDate,
            PlayCount = state.PlayCount,
            IsFavorite = state.IsFavorite,
            Rating = state.Rating,
            Likes = state.Likes
        };
    }

    private static bool IsWithin(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }
}
