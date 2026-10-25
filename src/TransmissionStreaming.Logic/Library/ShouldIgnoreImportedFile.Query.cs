namespace TransmissionStreaming.Logic.Library;

using Core.Configuration;
using Core.Filesystem;
using Core.Projection;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Decides whether native indexing would duplicate a still-owned streaming projection.</summary>
public static class ShouldIgnoreImportedFile
{
    /// <summary>Requests a synchronous resolver decision from local item and projection snapshots.</summary>
    public sealed record Query(string Path, bool IsDirectory) : IQuery<bool>;

    internal sealed class Handler(IOptionsMonitor<StreamingOptions> options, IProjectionStore store,
        IProjectionLibrary library, IMediaFiles files) : IQueryHandler<Query, bool>
    {
        public ValueTask<bool> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!options.CurrentValue.Enabled || query.IsDirectory) return ValueTask.FromResult(false);
            var entry = store.All().FirstOrDefault(candidate => candidate.CompletedPath == query.Path);
            if (entry is null || entry.HandoffPending || !files.Exists(entry.StreamPath) || library.Find(entry.StreamPath) is null)
                return ValueTask.FromResult(false);
            // An already indexed native item remains visible until its user history has been merged.
            return ValueTask.FromResult(library.Find(query.Path) is null);
        }
    }
}
