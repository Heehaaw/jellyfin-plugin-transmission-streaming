namespace TransmissionStreaming.Infrastructure.Arr.Imports;

using Core.Arr;

/// <summary>Proves that an imported file belongs to the original download.</summary>
internal interface IArrImportResolver
{
    Task<string?> FindImportedFileAsync(ArrConnection connection, MediaIdentity identity, long expectedSize, CancellationToken cancellationToken);
}
