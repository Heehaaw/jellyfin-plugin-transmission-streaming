namespace TransmissionStreaming.Core.Projection;

/// <summary>Durable projection identities and their current imported backing paths.</summary>
public interface IProjectionStore
{
    /// <summary>Loads persisted projection snapshots for discovery and reconciliation.</summary>
    IReadOnlyList<Projection> All();

    /// <summary>Returns recent successful native handoffs, newest first.</summary>
    IReadOnlyList<CompletedProjection> Completed();

    /// <summary>Atomically archives a successful handoff, retires its active projection and bounds retained history.</summary>
    /// <remarks>Repeating completion for the same identifier updates the existing history item.</remarks>
    void Complete(CompletedProjection entry, int historyLimit);

    /// <summary>Loads a projection by its permanent identifier, returning null when it has not been persisted.</summary>
    Projection? Get(string id);

    /// <summary>Persists the full projection snapshot, replacing the record with the same permanent identifier.</summary>
    /// <remarks>Callers retain the existing identifier and secret when updating download or import state.</remarks>
    void Save(Projection projection);

    /// <summary>Removes only the projection with this permanent identifier; missing records are unchanged.</summary>
    void Remove(string id);
}
