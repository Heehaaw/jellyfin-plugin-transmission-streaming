namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Projection;

/// <summary>Jellyfin-specific index lookup used to avoid duplicate imports.</summary>
internal interface IProjectionIndex : IProjectionLibrary
{
    /// <summary>Checks whether Jellyfin already owns a database entry for the exact local media path.</summary>
    bool IsPathIndexed(string path);
    /// <summary>Checks that the permanent stream entry exists before a completed native file can be hidden.</summary>
    bool IsIndexed(Projection entry);
}
