namespace TransmissionStreaming.Infrastructure.Tests.Filesystem;

public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }
    public void Dispose()
    {
        // xUnit also disposes generated arguments after the test's local scope exits.
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}
