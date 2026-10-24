namespace TransmissionStreaming.Core.Projection;

/// <summary>Durable projection identities and their current imported backing paths.</summary>
public interface IProjectionStore
{
    IReadOnlyList<Projection> All();
    Projection? Get(string id);
    void Save(Projection projection);
}
