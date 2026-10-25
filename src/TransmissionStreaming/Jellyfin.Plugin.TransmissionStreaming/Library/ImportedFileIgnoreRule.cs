namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Logic.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Adapts Jellyfin's synchronous resolver hook to the local indexing policy query.</summary>
internal sealed class ImportedFileIgnoreRule(IServiceScopeFactory scopeFactory) : IResolverIgnoreRule
{
    public bool ShouldIgnore(FileSystemMetadata fileInfo, BaseItem? parent)
    {
        // The singleton hook runs outside HTTP requests and owns a short synchronous scope.
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new ShouldIgnoreImportedFile.Query(fileInfo.FullName, fileInfo.IsDirectory);
        var decision = mediator.Send(query);
        return decision.GetAwaiter().GetResult();
    }
}
