namespace TransmissionStreaming.Infrastructure.Filesystem;

using Core.Filesystem;

/// <summary>Applies a selected root pair and checks the physical filesystem for escaping aliases.</summary>
internal sealed class PhysicalMediaPathMapper : IMediaPathMapper
{
    public string? Map(string path, string remoteRoot, string localRoot)
    {
        return new PathMapping(remoteRoot, localRoot).Map(path);
    }
}
