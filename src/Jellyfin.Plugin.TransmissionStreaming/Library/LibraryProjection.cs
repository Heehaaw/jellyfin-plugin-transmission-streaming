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
    ISessionManager sessions
) : IProjectionLibrary
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
        if (item is null)
        {
            return null;
        }

        var itemId = item.Id.ToString("N");
        var isPlaying = sessions.Sessions.Any(session => session.NowPlayingItem?.Id == item.Id);
        return new LibraryItem(itemId, item.Name, item.Overview, isPlaying);
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
            var userId = user.Id.ToString("N");
            var sourceSnapshot = Snapshot(sourceState);
            var targetSnapshot = Snapshot(targetState);
            var userState = new LibraryUserState(userId, sourceSnapshot, targetSnapshot);
            result.Add(userState);
        }

        return result;
    }

    public void SaveUserState(string userId, string itemId, LibraryWatchState state)
    {
        var targetUserId = Guid.Parse(userId);
        var user = users.GetUsers().FirstOrDefault(candidate => candidate.Id == targetUserId)
                   ?? throw new InvalidOperationException("The user is unavailable for history transfer.");
        var item = RequiredItem(itemId);
        var update = new UpdateUserItemDataDto
        {
            PlaybackPositionTicks = state.PlaybackPositionTicks,
            Played = state.Played,
            LastPlayedDate = state.LastPlayedDate,
            PlayCount = state.PlayCount,
            IsFavorite = state.IsFavorite,
            Rating = state.Rating,
            Likes = state.Likes
        };
        userData.SaveUserData(user, item, update, UserDataSaveReason.Import);
    }

    public void Remove(string itemId)
    {
        var parsedId = Guid.Parse(itemId);
        var item = library.GetItemById(parsedId);
        if (item is null)
        {
            return;
        }

        var deleteOptions = new DeleteOptions
        {
            DeleteFileLocation = false
        };
        library.DeleteItem(item, deleteOptions);
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
                var progress = new Progress<double>();
                await item.ValidateChildren(progress, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private BaseItem RequiredItem(string id)
    {
        var parsedId = Guid.Parse(id);
        return library.GetItemById(parsedId) ?? throw new InvalidOperationException("The library item is unavailable.");
    }

    private BaseItem? FindNative(string path)
    {
        var query = new InternalItemsQuery
        {
            Path = path,
            Recursive = true
        };
        var match = library.GetItemList(query)
            .FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.Ordinal));
        return match is null ? null : library.GetItemById(match.Id) ?? match;
    }

    private static LibraryWatchState? Snapshot(UserItemData? state)
    {
        return state is null
            ? null
            : new LibraryWatchState
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
