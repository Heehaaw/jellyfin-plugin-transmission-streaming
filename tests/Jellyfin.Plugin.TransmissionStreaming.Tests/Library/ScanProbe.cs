namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

/// <summary>Observes the public folder-validation wrapper without calling Jellyfin's static host services.</summary>
internal sealed class ScanProbe : AggregateFolder
{
    public ScanProbe() { }

    public int ScanCount { get; private set; }
    public bool? Recursive { get; private set; }
    public CancellationToken CancellationToken { get; private set; }

    protected override Task ValidateChildrenInternal(
        IProgress<double> progress,
        bool recursive,
        bool refreshChildMetadata,
        bool allowRemoveRoot,
        MetadataRefreshOptions refreshOptions,
        IDirectoryService directoryService,
        CancellationToken cancellationToken
    )
    {
        ScanCount++;
        Recursive = recursive;
        CancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}
