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
        TimeProvider timeProvider
    ) : ICommandHandler<Command, bool>
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

            if (string.IsNullOrWhiteSpace(entry.CompletedPath) || activity.Count(entry.Id) > 0)
            {
                return false;
            }

            var importQuery = new ResolveImportedFile.Query(entry);
            var importedPath = await mediator.Send(importQuery, cancellationToken).ConfigureAwait(false);
            if (importedPath is null)
            {
                return false;
            }

            entry.CompletedPath = importedPath;
            var projectedItem = library.Find(entry.StreamPath);
            if (projectedItem?.IsPlaying == true)
            {
                return false;
            }

            var nativeItem = await FindOrIndexNativeAsync(entry, importedPath, cancellationToken).ConfigureAwait(false);
            if (nativeItem is null || nativeItem.Id == projectedItem?.Id || nativeItem.IsPlaying || IsProjectionInUse(entry))
            {
                return false;
            }

            await PrepareNativeItemAsync(projectedItem, nativeItem, cancellationToken).ConfigureAwait(false);

            // Host writes can yield. Retain the source if playback started during the transfer.
            var nativeIsPlaying = library.Find(importedPath)?.IsPlaying == true;
            if (IsProjectionInUse(entry) || nativeIsPlaying)
            {
                return false;
            }

            RetireProjection(entry, projectedItem, nativeItem);
            return true;
        }

        private async Task<LibraryItem?> FindOrIndexNativeAsync(Projection entry, string importedPath, CancellationToken cancellationToken)
        {
            if (!entry.HandoffPending)
            {
                // Persist intent before scanning; the resolver reads this record to allow native indexing.
                entry.HandoffPending = true;
                store.Save(entry);
            }

            var nativeItem = library.Find(importedPath);
            if (nativeItem is not null)
            {
                return nativeItem;
            }

            await library.IndexImportedAsync(importedPath, cancellationToken).ConfigureAwait(false);
            return library.Find(importedPath);
        }

        private bool IsProjectionInUse(Projection entry)
        {
            // Scanning or history writes can take time; refresh the facts before retiring the source.
            return activity.Count(entry.Id) > 0 || library.Find(entry.StreamPath)?.IsPlaying == true;
        }

        private async Task PrepareNativeItemAsync(LibraryItem? projectedItem, LibraryItem nativeItem, CancellationToken cancellationToken)
        {
            if (projectedItem is not null)
            {
                var transfer = new TransferUserHistory.Command(projectedItem.Id, nativeItem.Id);
                await mediator.Send(transfer, cancellationToken).ConfigureAwait(false);
            }

            var synopsis = string.IsNullOrWhiteSpace(nativeItem.Overview) ? projectedItem?.Overview : nativeItem.Overview;
            var overview = overviews.Format(synopsis, options.CurrentValue.JellyfinPublicUrl);
            if (nativeItem.Overview == overview)
            {
                return;
            }

            await library.UpdateMetadataAsync(nativeItem.Id, nativeItem.Name, overview, cancellationToken).ConfigureAwait(false);
        }

        private void RetireProjection(Projection entry, LibraryItem? projectedItem, LibraryItem nativeItem)
        {
            // Every user save must succeed first. File or archive failures leave durable intent for retry.
            if (projectedItem is not null)
            {
                library.Remove(projectedItem.Id);
            }

            files.RemoveFiles(entry);
            var identity = entry.Identity;
            var title = identity.Kind == MediaKind.Episode
                ? $"{identity.Title} · S{identity.SeasonNumber:00}E{identity.EpisodeNumber:00} · {identity.EpisodeTitle}"
                : identity.Title;
            var completedAt = timeProvider.GetUtcNow();
            var completion = new CompletedProjection(entry.Id, title, nativeItem.Id, completedAt);
            store.Complete(completion, CompletedHistoryLimit);
        }
    }
}
