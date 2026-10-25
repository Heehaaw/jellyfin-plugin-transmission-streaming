namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Logic.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;
using Mediator;

/// <summary>Adapts Jellyfin's synchronous resolver hook to the local indexing policy query.</summary>
internal sealed class ImportedFileIgnoreRule(IMediator mediator) : IResolverIgnoreRule
{
    public bool ShouldIgnore(FileSystemMetadata fileInfo, BaseItem? parent)
    {
        // The host hook is synchronous; this query reads only synchronous local snapshots.
        return mediator.Send(new ShouldIgnoreImportedFile.Query(fileInfo.FullName, fileInfo.IsDirectory)).GetAwaiter().GetResult();
    }
}
