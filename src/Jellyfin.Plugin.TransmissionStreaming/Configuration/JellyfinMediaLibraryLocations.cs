namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

/// <summary>Exposes media roots without leaking Jellyfin types into application inference.</summary>
internal sealed class JellyfinMediaLibraryLocations(ILibraryManager library) : IMediaLibraryLocations
{
    public IReadOnlyList<string> GetPaths()
    {
        return library.GetVirtualFolders(false)
            .Where(folder => folder.CollectionType is CollectionTypeOptions.movies or CollectionTypeOptions.tvshows)
            .SelectMany(folder => folder.Locations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
