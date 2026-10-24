namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;

/// <summary>Prevents an imported file from duplicating a permanent projected item.</summary>
internal sealed class ImportedFileIgnoreRule(
    IStreamingOptionsProvider configuration,
    IProjectionStore store,
    IProjectionIndex projection) : IResolverIgnoreRule
{
    public bool ShouldIgnore(FileSystemMetadata fileInfo, BaseItem? parent)
    {
        if (!configuration.Get().Enabled || fileInfo.IsDirectory)
        {
            return false;
        }

        var managed = store.All().FirstOrDefault(entry => entry.CompletedPath == fileInfo.FullName);
        if (managed is null || !File.Exists(managed.StreamPath) || !projection.IsIndexed(managed))
        {
            return false;
        }

        // Existing native entries stay visible until explicit reconciliation copies history.
        // A concurrent scan must not remove them as missing before that merge.
        return !projection.IsPathIndexed(fileInfo.FullName);
    }
}
