namespace TransmissionStreaming.Logic.Configuration;

using Core.Configuration;
using Core.Filesystem;
using Mediator;

/// <summary>Confines a previously resolved local import to the owning service's current media roots.</summary>
public static class ValidateLocalMediaPath
{
    /// <summary>Requests the confined local path, returning null when it is no longer allowed.</summary>
    public sealed record Query(MediaService Service, string Path) : IQuery<string?>;

    internal sealed class Handler(IMediator mediator, IMediaPathMapper mapper) : IQueryHandler<Query, string?>
    {
        public async ValueTask<string?> Handle(Query query, CancellationToken cancellationToken)
        {
            var mappings = await mediator.Send(new GetServicePathMappings.Query(query.Service), cancellationToken).ConfigureAwait(false);
            foreach (var mapping in mappings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = mapping.LocalRoot.TrimEnd('/');
                if (root.StartsWith('/') && (query.Path == root || query.Path.StartsWith(root + "/", StringComparison.Ordinal)) &&
                    mapper.Map(query.Path, root, root) is { } local)
                {
                    return local;
                }
            }

            return null;
        }
    }
}
