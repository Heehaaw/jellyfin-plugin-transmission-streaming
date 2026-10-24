namespace TransmissionStreaming.Logic.Configuration;

using Core.Configuration;
using Core.Filesystem;
using Mediator;

/// <summary>Applies the most specific unambiguous service root before physical confinement.</summary>
public static class MapServicePath
{
    /// <summary>Requests a local path for a directory or file reported by the specified service.</summary>
    public sealed record Query(MediaService Service, string Path) : IQuery<string?>;

    internal sealed class Handler(IMediator mediator, IMediaPathMapper mapper) : IQueryHandler<Query, string?>
    {
        public async ValueTask<string?> Handle(Query query, CancellationToken cancellationToken)
        {
            var mappings = await mediator.Send(new GetServicePathMappings.Query(query.Service), cancellationToken).ConfigureAwait(false);
            var candidates = mappings.Where(mapping => Contains(mapping.RemoteRoot, query.Path))
                .OrderByDescending(mapping => mapping.RemoteRoot.TrimEnd('/').Length).ToArray();
            if (candidates.Length == 0)
            {
                return null;
            }

            var length = candidates[0].RemoteRoot.TrimEnd('/').Length;
            var closest = candidates.Where(mapping => mapping.RemoteRoot.TrimEnd('/').Length == length).ToArray();
            if (closest.Select(mapping => mapping.LocalRoot.TrimEnd('/')).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                return null;
            }
            return mapper.Map(query.Path, closest[0].RemoteRoot, closest[0].LocalRoot);
        }

        private static bool Contains(string root, string path)
        {
            var normalized = root.TrimEnd('/');
            return normalized.StartsWith('/') && (path == normalized || path.StartsWith(normalized + "/", StringComparison.Ordinal));
        }
    }
}
