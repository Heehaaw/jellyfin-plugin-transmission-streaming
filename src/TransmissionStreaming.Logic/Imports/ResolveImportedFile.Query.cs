namespace TransmissionStreaming.Logic.Imports;

using Mediator;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Configuration;

/// <summary>Defines the ResolveImportedFile query and its application handler.</summary>
public static class ResolveImportedFile
{
    /// <summary>Resolves the exact imported backing file without changing the managed projection.</summary>
    public sealed record Query(Projection Projection) : IQuery<string?>;

    /// <summary>Accepts only an arr-confirmed import within the mapped media root and expected byte length.</summary>
    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IArrClient arr,
        IMediator mediator,
        IMediaFiles media
    ) : IQueryHandler<Query, string?>
    {
        public async ValueTask<string?> Handle(Query query, CancellationToken cancellationToken)
        {
            var configuration = options.CurrentValue;
            var entry = query.Projection;
            var service = entry.Identity.Kind == MediaKind.Movie ? MediaService.Radarr : MediaService.Sonarr;
            if (entry.CompletedPath is { } completed)
            {
                var validatePath = new ValidateLocalMediaPath.Query(service, completed);
                var mapped = await mediator.Send(validatePath, cancellationToken).ConfigureAwait(false);
                return mapped is not null && media.HasLength(mapped, entry.Length) ? mapped : null;
            }

            var movie = entry.Identity.Kind == MediaKind.Movie;
            var url = movie ? configuration.RadarrUrl : configuration.SonarrUrl;
            var key = movie ? configuration.RadarrApiKey : configuration.SonarrApiKey;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var connection = new ArrConnection
            {
                Url = url,
                ApiKey = key
            };
            var remote = await arr.FindImportedFileAsync(connection, entry.Identity, entry.Length, cancellationToken).ConfigureAwait(false);
            if (remote is null)
            {
                return null;
            }

            var mapPath = new MapServicePath.Query(service, remote);
            var local = await mediator.Send(mapPath, cancellationToken).ConfigureAwait(false);
            return local is not null && media.HasLength(local, entry.Length) ? local : null;
        }
    }
}
