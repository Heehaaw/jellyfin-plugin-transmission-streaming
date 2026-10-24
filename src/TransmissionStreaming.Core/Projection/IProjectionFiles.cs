namespace TransmissionStreaming.Core.Projection;

/// <summary>Filesystem projection of permanent streaming entries.</summary>
public interface IProjectionFiles
{
    /// <summary>Gets the configured dedicated projection root, or the plugin-owned default when no explicit root is set.</summary>
    string Root { get; }

    /// <summary>Claims an empty dedicated root and creates the movie and show folders, or reopens a previously owned root.</summary>
    /// <exception cref="InvalidOperationException">The root is the filesystem root or contains unrelated files on first use.</exception>
    void EnsureReady();

    /// <summary>Writes the stable stream link and metadata sidecars, replacing their contents only when they change.</summary>
    /// <remarks>Generated stream links contain capability secrets and must be treated as private.</remarks>
    void WriteFiles(Projection entry);
}
