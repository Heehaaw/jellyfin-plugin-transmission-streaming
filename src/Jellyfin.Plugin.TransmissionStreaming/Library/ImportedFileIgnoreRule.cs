namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using Jellyfin.Plugin.TransmissionStreaming;
using Jellyfin.Plugin.TransmissionStreaming.Persistence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;


/// <summary>Prevents imported media from creating a second copy of an existing projected title.</summary>
public sealed class ImportedFileIgnoreRule(Plugin plugin, ProjectionStore store, LibraryProjection projection) : IResolverIgnoreRule
{
    public bool ShouldIgnore(FileSystemMetadata fileInfo, BaseItem? parent)
    {
        if (!plugin.Configuration.Enabled || fileInfo.IsDirectory) return false;
        var managed = store.All().FirstOrDefault(entry => entry.CompletedPath == fileInfo.FullName);
        if (managed is null || !File.Exists(managed.StreamPath) || !projection.IsIndexed(managed)) return false;

        // An already-indexed native entry must remain visible until the worker has copied
        // every user's state. Otherwise a concurrent scan could remove it before that merge.
        return !projection.IsPathIndexed(fileInfo.FullName);
    }
}
