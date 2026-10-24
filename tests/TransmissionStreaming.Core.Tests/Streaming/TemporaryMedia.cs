namespace TransmissionStreaming.Core.Tests.Streaming;




public sealed class TemporaryMedia : IDisposable
{
    public TemporaryMedia(byte[] seed)
    {
        Content = Enumerable.Range(0, 4096).Select(i => seed[i % seed.Length]).ToArray();
        Path = System.IO.Path.GetTempFileName();
        File.WriteAllBytes(Path, Content);
    }

    public string Path { get; }
    public byte[] Content { get; }
    public void Dispose() => File.Delete(Path);
}
