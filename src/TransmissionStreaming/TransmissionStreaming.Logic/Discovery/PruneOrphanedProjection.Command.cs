namespace TransmissionStreaming.Logic.Discovery;

using Core.Arr;
using Core.Configuration;
using Core.Filesystem;
using Core.Projection;
using Core.Streaming;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Removes a projection only after both its download and arr ownership have disappeared.</summary>
public static class PruneOrphanedProjection
{
    /// <summary>Requests cleanup using the current successful Transmission snapshot.</summary>
    public sealed record Command(Projection Entry, bool TorrentPresent) : ICommand<bool>;

    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IMediator mediator,
        IMediaFiles media,
        IStreamActivity activity,
        IProjectionLibrary library,
        IProjectionFiles files,
        IProjectionStore store
    ) : ICommandHandler<Command, bool>
    {
        public async ValueTask<bool> Handle(Command command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = command.Entry;
            if (command.TorrentPresent || activity.Count(entry.Id) > 0 ||
                !string.IsNullOrWhiteSpace(entry.CompletedPath) && media.Exists(entry.CompletedPath))
            {
                return false;
            }

            var config = options.CurrentValue;
            var connection = entry.Identity.Kind switch
            {
                MediaKind.Movie => new ArrConnection
                {
                    Url = config.RadarrUrl,
                    ApiKey = config.RadarrApiKey
                },
                MediaKind.Episode => new ArrConnection
                {
                    Url = config.SonarrUrl,
                    ApiKey = config.SonarrApiKey
                },
                _ => null
            };
            if (connection is null || string.IsNullOrWhiteSpace(connection.Url) || string.IsNullOrWhiteSpace(connection.ApiKey))
            {
                return false;
            }

            // A missing torrent alone can mean a late import. Require an authoritative
            // arr library response; connection failures propagate without deleting anything.
            if (await mediator.Send(new IsArrMediaTracked.Query(connection, entry.Identity), cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            var projected = library.Find(entry.StreamPath);
            if (projected?.IsPlaying == true)
            {
                return false;
            }

            if (projected is not null)
            {
                library.Remove(projected.Id);
            }

            // Keep the durable record until host and file cleanup succeed, so a partial failure retries.
            files.RemoveFiles(entry);
            store.Remove(entry.Id);
            return true;
        }
    }
}
