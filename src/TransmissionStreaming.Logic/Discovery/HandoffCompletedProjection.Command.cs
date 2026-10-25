namespace TransmissionStreaming.Logic.Discovery;

using Core.Arr;
using Core.Configuration;
using Core.Projection;
using Core.Streaming;
using Imports;
using Library;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Retires a completed proxy once the normal library can own playback and user history.</summary>
public static class HandoffCompletedProjection
{
    private const int CompletedHistoryLimit = 20;

    /// <summary>Requests handoff using the current successful Transmission snapshot.</summary>
    public sealed record Command(Projection Entry, bool TorrentPresent) : ICommand<bool>;

    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IMediator mediator,
        IStreamActivity activity,
        IProjectionLibrary library,
        IProjectionOverview overviews,
        IProjectionFiles files,
        IProjectionStore store,
        TimeProvider timeProvider) : ICommandHandler<Command, bool>
    {
        public async ValueTask<bool> Handle(Command command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = command.Entry;
            if (command.TorrentPresent)
            {
                entry.HandoffPending = false;
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.CompletedPath) || activity.Count(entry.Id) > 0) return false;
            var imported = await mediator.Send(new ResolveImportedFile.Query(entry), cancellationToken).ConfigureAwait(false);
            if (imported is null) return false;
            entry.CompletedPath = imported;

            var projected = library.Find(entry.StreamPath);
            if (projected?.IsPlaying == true) return false;
            if (!entry.HandoffPending)
            {
                // Persist indexing intent before the host scan; the resolver reads this record.
                entry.HandoffPending = true;
                store.Save(entry);
            }
            var native = library.Find(imported);
            if (native is null)
            {
                await library.IndexImportedAsync(imported, cancellationToken).ConfigureAwait(false);
                native = library.Find(imported);
            }
            if (native is null || native.Id == projected?.Id || native.IsPlaying) return false;

            // A host scan can take time; playback may have started since the first snapshot.
            if (activity.Count(entry.Id) > 0 || library.Find(entry.StreamPath)?.IsPlaying == true) return false;

            if (projected is not null)
            {
                await mediator.Send(new TransferUserHistory.Command(projected.Id, native.Id), cancellationToken).ConfigureAwait(false);
            }
            var synopsis = string.IsNullOrWhiteSpace(native.Overview) ? projected?.Overview : native.Overview;
            var overview = overviews.Format(synopsis, options.CurrentValue.JellyfinPublicUrl);
            if (native.Overview != overview)
            {
                await library.UpdateMetadataAsync(native.Id, native.Name, overview, cancellationToken).ConfigureAwait(false);
            }

            // History and metadata writes can yield too. Retain the source if playback started during them.
            if (activity.Count(entry.Id) > 0 || library.Find(entry.StreamPath)?.IsPlaying == true || library.Find(imported)?.IsPlaying == true) return false;

            // Keep the source until every user save succeeds; partial failures retry without losing ownership.
            if (projected is not null) library.Remove(projected.Id);
            files.RemoveFiles(entry);
            var identity = entry.Identity;
            var title = identity.Kind == MediaKind.Episode
                ? $"{identity.Title} · S{identity.SeasonNumber:00}E{identity.EpisodeNumber:00} · {identity.EpisodeTitle}"
                : identity.Title;
            store.Complete(new CompletedProjection(entry.Id, title, native.Id, timeProvider.GetUtcNow()), CompletedHistoryLimit);
            return true;
        }
    }
}
