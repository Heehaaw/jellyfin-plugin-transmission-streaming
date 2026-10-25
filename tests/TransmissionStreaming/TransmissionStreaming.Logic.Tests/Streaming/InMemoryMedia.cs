namespace TransmissionStreaming.Logic.Tests.Streaming;

using Moq;
using TransmissionStreaming.Core.Filesystem;

/// <summary>Generated media bytes exposed through the filesystem port, without touching a physical directory.</summary>
internal sealed class InMemoryMedia(string path, byte[] seed)
{
    public string Path { get; } = path;

    public byte[] Content { get; } = Enumerable.Range(0, 4096).Select(index => seed[index % seed.Length]).ToArray();

    public void SetupReads(Mock<IMediaFiles> files, string? backingPath = null)
    {
        files.Setup(value => value.ReadAsync(backingPath ?? Path, It.IsAny<Memory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Memory<byte> buffer, long offset, CancellationToken cancellationToken) => ReadAsync(buffer, offset, cancellationToken));
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = (int)Math.Min(buffer.Length, Math.Max(0, Content.Length - offset));
        Content.AsMemory((int)offset, count).CopyTo(buffer);
        return ValueTask.FromResult(count);
    }
}
