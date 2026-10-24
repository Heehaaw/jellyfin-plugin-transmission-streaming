namespace TransmissionStreaming.Core.Streaming;

/// <summary>Tracks active reads without retaining their video data.</summary>
public interface IStreamActivity
{
    /// <summary>Returns the number of outstanding reader leases for a projection.</summary>
    int Count(string id);

    /// <summary>Registers a reader until the returned lease is disposed.</summary>
    /// <remarks>The caller disposes the lease on completion, cancellation, or failure. Repeated disposal releases it only once.</remarks>
    IDisposable Open(string id);
}
