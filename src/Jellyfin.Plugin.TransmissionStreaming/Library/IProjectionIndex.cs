namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Core.Projection;

/// <summary>Jellyfin-specific index lookup used to avoid duplicate imports.</summary>
internal interface IProjectionIndex : IProjectionLibrary
{
    bool IsPathIndexed(string path);
    bool IsIndexed(Projection entry);
}
