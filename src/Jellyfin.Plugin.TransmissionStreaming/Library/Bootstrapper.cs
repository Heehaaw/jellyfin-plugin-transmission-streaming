namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using MediaBrowser.Controller.Resolvers;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers projection ownership and per-user history reconciliation.</summary>
internal static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<MediaPaths>();
        services.AddSingleton<WatchStateMerger>();
        services.AddSingleton<LibraryProjection>();
        services.AddSingleton<IResolverIgnoreRule, ImportedFileIgnoreRule>();
    }
}
