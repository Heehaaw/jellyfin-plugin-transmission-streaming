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
    /// <summary>Resolves the exact imported backing file without changing the permanent projection.</summary>
    public sealed record Query(Projection Projection) : IQuery<string?>;

    /// <summary>Accepts only an arr-confirmed import within the mapped media root and expected byte length.</summary>
    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IArrClient arr,
        IMediator mediator,
        IMediaFiles media) : IQueryHandler<Query, string?>
    {
        public async ValueTask<string?> Handle(Query query, CancellationToken cancellationToken)
        {
            var config = options.CurrentValue;
            var entry = query.Projection;
            var service = entry.Identity.Kind == MediaKind.Movie ? MediaService.Radarr : MediaService.Sonarr;
            if (entry.CompletedPath is { } completed)
            {
                var mapped = await mediator.Send(new ValidateLocalMediaPath.Query(service, completed), cancellationToken).ConfigureAwait(false);
                return mapped is not null && media.HasLength(mapped, entry.Length) ? mapped : null;
            }

            var movie = entry.Identity.Kind == MediaKind.Movie;
            var url = movie ? config.RadarrUrl : config.SonarrUrl;
            var key = movie ? config.RadarrApiKey : config.SonarrApiKey;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var connection = new ArrConnection { Url = url, ApiKey = key };
            var remote = await arr.FindImportedFileAsync(connection, entry.Identity, entry.Length, cancellationToken).ConfigureAwait(false);
            if (remote is null)
            {
                return null;
            }

            var local = await mediator.Send(new MapServicePath.Query(service, remote), cancellationToken).ConfigureAwait(false);
            return local is not null && media.HasLength(local, entry.Length) ? local : null;
        }
    }
}
