namespace TransmissionStreaming.Logic.Library;

using Core.Arr;
using Core.Configuration;
using Core.Projection;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Maintains projected metadata and protects watch history during the seeding phase.</summary>
public static class ReconcileProjection
{
    /// <summary>Reconciles the current projection without moving its backing media.</summary>
    public sealed record Command(Projection Entry) : ICommand;

    internal sealed class Handler(IOptionsMonitor<StreamingOptions> options, IProjectionLibrary library,
        IProjectionOverview overviews, IMediator mediator) : ICommandHandler<Command>
    {
        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = command.Entry;
            var projected = library.Find(entry.StreamPath);
            if (projected is null) return Unit.Value;
            var identity = entry.Identity;
            var config = options.CurrentValue;
            var title = identity.Kind == MediaKind.Episode && !string.IsNullOrWhiteSpace(identity.EpisodeTitle) ? identity.EpisodeTitle : identity.Title;
            var display = config.ShowDownloadStatus && entry.CompletedPath is null ? $"{title} [{entry.Status}]" : title;
            var overview = overviews.Format(projected.Overview, config.JellyfinPublicUrl);
            if (projected.Name != display || projected.Overview != overview)
            {
                await library.UpdateMetadataAsync(projected.Id, display, overview, cancellationToken).ConfigureAwait(false);
            }

            if (entry.CompletedPath is null || entry.HandoffPending) return Unit.Value;
            var native = library.Find(entry.CompletedPath);
            if (native is null || native.Id == projected.Id || native.IsPlaying) return Unit.Value;
            await mediator.Send(new TransferUserHistory.Command(native.Id, projected.Id), cancellationToken).ConfigureAwait(false);
            library.Remove(native.Id);
            return Unit.Value;
        }
    }
}
