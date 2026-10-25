namespace TransmissionStreaming.Core.Streaming;

/// <summary>A selected byte interval and whether it represents a partial response.</summary>
public readonly record struct ByteRange(long Start, long Count, bool Partial);
