namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Projection;
using MediaBrowser.Controller.Resolvers;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers only Jellyfin-specific indexing and watch-state adapters.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddJellyfinLibrary(this IServiceCollection services)
    {
        return services
            .AddSingleton<IWatchStateMerger, WatchStateMerger>()
            .AddSingleton<IProjectionIndex, LibraryProjection>()
            .AddSingleton<IProjectionLibrary>(provider => provider.GetRequiredService<IProjectionIndex>())
            .AddSingleton<IResolverIgnoreRule, ImportedFileIgnoreRule>();
    }
}
