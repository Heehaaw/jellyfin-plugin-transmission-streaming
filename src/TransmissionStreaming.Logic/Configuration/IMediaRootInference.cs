namespace TransmissionStreaming.Logic.Configuration;

/// <summary>Infers a local mount root from configured library folders without examining media files.</summary>
internal interface IMediaRootInference
{
    string? Infer(IReadOnlyList<string> libraryPaths, string projectionRoot);
}
