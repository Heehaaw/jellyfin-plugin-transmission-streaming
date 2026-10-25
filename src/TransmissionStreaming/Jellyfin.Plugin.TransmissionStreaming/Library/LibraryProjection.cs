namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Projection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

/// <summary>Maps Jellyfin indexing, metadata, sessions, and user-data APIs onto host-neutral contracts.</summary>
internal sealed class LibraryProjection(
    IProjectionFiles files,
    ILibraryManager library,
    IUserManager users,
    IUserDataManager userData,
    ISessionManager sessions,
    IFileSystem fileSystem
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
        var folders = library.GetVirtualFolders(false);
        var locations = folders
            .SelectMany(folder => folder.Locations)
            .Where(location => IsWithin(location, path) || includeDescendants && IsWithin(path, location))
            .ToHashSet(StringComparer.Ordinal);
        var directoryService = new DirectoryService(fileSystem);
        var refreshOptions = new MetadataRefreshOptions(directoryService);
        var progress = new Progress<double>();
        foreach (var location in locations)
        {
            // CollectionFolder validation is a no-op; scan its actual filesystem roots instead.
            var physicalFolder = await FindPhysicalFolderAsync(location, progress, refreshOptions, cancellationToken).ConfigureAwait(false);
            if (physicalFolder is null)
            {
                continue;
            }

            await physicalFolder.ValidateChildren(progress, refreshOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        foreach (var folder in folders.Where(folder => folder.Locations.Any(locations.Contains)))
        {
            await RefreshCollectionAsync(folder.ItemId, refreshOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Folder?> FindPhysicalFolderAsync(string location, IProgress<double> progress, MetadataRefreshOptions refreshOptions, CancellationToken cancellationToken)
    {
        if (library.FindByPath(location, true) is Folder physicalFolder)
        {
            return physicalFolder;
        }

        // Empty library roots may have no indexed Folder. Register newly populated roots without scanning other libraries.
        var root = library.RootFolder;
        await root.ValidateChildren(progress, refreshOptions, recursive: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        return library.FindByPath(location, true) as Folder;
    }

    private async Task RefreshCollectionAsync(string itemId, MetadataRefreshOptions refreshOptions, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(itemId, out var id) || library.GetItemById(id) is not CollectionFolder collection)
        {
            return;
        }

        // A collection caches its physical root IDs separately from each folder's children.
        await collection.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
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
