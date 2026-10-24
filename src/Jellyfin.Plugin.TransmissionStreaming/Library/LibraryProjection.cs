namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;

/// <summary>Adapts permanent application projections to Jellyfin indexing and per-user history.</summary>
internal sealed class LibraryProjection(
    IStreamingOptionsProvider configuration,
    IProjectionFiles files,
    ILibraryManager library,
    IUserManager users,
    IUserDataManager userData,
    ISessionManager sessions,
    IWatchStateMerger watchStates) : IProjectionIndex
{
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Only libraries explicitly configured to contain the projection are refreshed.
        // Before such a library exists, projection files remain ready for the administrator.
        var root = Path.GetFullPath(files.Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var folder in library.GetVirtualFolders(false))
        {
            if (!folder.Locations.Any(location => IsWithin(root, location) || IsWithin(location, root)))
            {
                continue;
            }

            if (Guid.TryParse(folder.ItemId, out var id) && library.GetItemById(id) is Folder item)
            {
                await item.ValidateChildren(new Progress<double>(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task ReconcileAsync(Projection entry, CancellationToken cancellationToken)
    {
        var projected = Find(entry.StreamPath);
        if (projected is null)
        {
            return;
        }

        var identity = entry.Identity;
        var title = identity.Kind == MediaKind.Episode && !string.IsNullOrWhiteSpace(identity.EpisodeTitle)
            ? identity.EpisodeTitle : identity.Title;
        var display = configuration.Get().ShowDownloadStatus && entry.CompletedPath is null
            ? $"{title} [{entry.Status}]" : title;
        if (projected.Name != display && projected.GetParent() is { } parent)
        {
            projected.Name = display;
            await library.UpdateItemAsync(projected, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        if (entry.CompletedPath is null)
        {
            return;
        }

        var native = Find(entry.CompletedPath);
        if (native is null || native.Id == projected.Id || IsPlaying(native.Id))
        {
            return;
        }

        // An arr import can be indexed before the next poll. Merge every user's state
        // before removing that duplicate database entry; never delete the media itself.
        foreach (var user in users.GetUsers())
        {
            // Path queries do not necessarily hydrate user data. The batch API loads saved
            // rows; the single-item accessor can otherwise return an empty default here.
            var saved = userData.GetUserDataBatch([native, projected], user);
            if (!saved.TryGetValue(native.Id, out var source))
            {
                continue;
            }

            saved.TryGetValue(projected.Id, out var target);
            userData.SaveUserData(user, projected, watchStates.Merge(source, target), UserDataSaveReason.Import);
        }
        library.DeleteItem(native, new DeleteOptions { DeleteFileLocation = false });
    }

    public bool IsPathIndexed(string path)
    {
        return Find(path) is not null;
    }

    public bool IsIndexed(Projection entry)
    {
        return IsPathIndexed(entry.StreamPath);
    }

    private bool IsPlaying(Guid id)
    {
        return sessions.Sessions.Any(session => session.NowPlayingItem?.Id == id);
    }

    private BaseItem? Find(string path)
    {
        var match = library.GetItemList(new InternalItemsQuery { Path = path, Recursive = true })
            .FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.Ordinal));
        // Update the canonical cached item so API reads see the imported user data immediately.
        return match is null ? null : library.GetItemById(match.Id) ?? match;
    }

    private static bool IsWithin(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }
}
