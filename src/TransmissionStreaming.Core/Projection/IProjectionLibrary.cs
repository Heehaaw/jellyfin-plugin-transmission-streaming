namespace TransmissionStreaming.Core.Projection;

/// <summary>Host operations required to index and reconcile permanent stream entries.</summary>
public interface IProjectionLibrary
{
    /// <summary>Asks the host to index libraries containing the dedicated projection folder.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>Updates the projected display state and merges a completed native duplicate's user history before removing its database entry.</summary>
    /// <remarks>Media files are retained, and an active native playback session may defer duplicate removal.</remarks>
    Task ReconcileAsync(Projection entry, CancellationToken cancellationToken);
}
