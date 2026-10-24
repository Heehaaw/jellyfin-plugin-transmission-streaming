namespace TransmissionStreaming.Core.Projection;

/// <summary>Durable projection identities and their current imported backing paths.</summary>
public interface IProjectionStore
{
    /// <summary>Loads persisted projection snapshots for discovery and reconciliation.</summary>
    IReadOnlyList<Projection> All();

    /// <summary>Loads a projection by its permanent identifier, returning null when it has not been persisted.</summary>
    Projection? Get(string id);

    /// <summary>Persists the full projection snapshot, replacing the record with the same permanent identifier.</summary>
    /// <remarks>Callers retain the existing identifier and secret when updating download or import state.</remarks>
    void Save(Projection projection);
}
