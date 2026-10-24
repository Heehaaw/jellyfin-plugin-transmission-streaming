namespace TransmissionStreaming.Core.Projection;

/// <summary>Host operations required to index and reconcile permanent stream entries.</summary>
public interface IProjectionLibrary
{
    Task RefreshAsync(CancellationToken cancellationToken);
    Task ReconcileAsync(Projection entry, CancellationToken cancellationToken);
}
