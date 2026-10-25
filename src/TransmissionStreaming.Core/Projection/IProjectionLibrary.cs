namespace TransmissionStreaming.Core.Projection;

/// <summary>Host operations required to index and reconcile permanent stream entries.</summary>
public interface IProjectionLibrary
{
    /// <summary>Asks the host to index libraries containing the dedicated projection folder.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>Updates the projected display state and merges a completed native duplicate's user history before removing its database entry.</summary>
    /// <remarks>Media files are retained, and an active native playback session may defer duplicate removal.</remarks>
    Task ReconcileAsync(Projection entry, CancellationToken cancellationToken);

    /// <summary>Removes only the projected host entry, returning false while that item is playing.</summary>
    /// <remarks>An already absent entry succeeds. Downloaded and imported media files are never deleted.</remarks>
    Task<bool> TryRemoveAsync(Projection entry, CancellationToken cancellationToken);
}
