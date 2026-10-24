namespace TransmissionStreaming.Infrastructure.Arr.Imports;

using Core.Arr;

/// <summary>Proves that an imported file belongs to the original download.</summary>
internal interface IArrImportResolver
{
    /// <summary>Returns a remote path only when import history and the current file agree with the original release.</summary>
    Task<string?> FindImportedFileAsync(ArrConnection connection, MediaIdentity identity, long expectedSize, CancellationToken cancellationToken);
}
