namespace TransmissionStreaming.Logic.Configuration;

using Core.Configuration;
using Core.Filesystem;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Discovers upstream directories and suggests mappings from the host's configured libraries.</summary>
public static class DiscoverServicePaths
{
    /// <summary>Uses the supplied connection, including unsaved edits, without persisting credentials or mappings.</summary>
    public sealed record Query(MediaService Service, ServiceConnection Connection) : IQuery<ServicePathSuggestions>
    {
        public override string ToString() => $"Discover paths for {Service} (credentials redacted)";
    }

    internal sealed class Handler(
        IServicePathDiscovery discovery,
        IOptionsMonitor<StreamingOptions> options,
        IMediaLibraryLocations libraries,
        IMediator mediator,
        IMediaPathMapper physicalPaths) : IQueryHandler<Query, ServicePathSuggestions>
    {
        public async ValueTask<ServicePathSuggestions> Handle(Query query, CancellationToken cancellationToken)
        {
            var discovered = await discovery.DiscoverAsync(query.Service, query.Connection, cancellationToken).ConfigureAwait(false);
            var remotePaths = query.Service == MediaService.Transmission
                ? discovered.DownloadDirectories.Concat(new[] { discovered.DownloadDirectory, discovered.IncompleteDirectory }.OfType<string>())
                : discovered.LibraryRoots;
            var roots = remotePaths.Where(Path.IsPathFullyQualified).Distinct(StringComparer.Ordinal).ToArray();
            var config = options.CurrentValue;
            var localPaths = (query.Service == MediaService.Transmission ? libraries.GetAllPaths() : libraries.GetServicePaths(query.Service))
                .Where(Path.IsPathFullyQualified)
                .Where(path => !Within(config.ProjectionRoot, path))
                .Distinct(StringComparer.Ordinal).ToArray();
            var mappings = new List<ServicePathMapping>();
            foreach (var remote in roots)
            {
                var local = await SuggestLocalAsync(query.Service, remote, roots.Length, localPaths, config, cancellationToken).ConfigureAwait(false);
                mappings.Add(new ServicePathMapping { Service = query.Service, RemoteRoot = remote, LocalRoot = local });
            }

            return new ServicePathSuggestions(discovered, RemoveCoveredMappings(mappings));
        }

        private IReadOnlyList<ServicePathMapping> RemoveCoveredMappings(IReadOnlyList<ServicePathMapping> mappings)
        {
            return mappings.Where(child => !mappings.Any(parent => Covers(parent, child))).ToArray();
        }
        
        private bool Covers(ServicePathMapping parent, ServicePathMapping child)
        {
            var remoteParent = parent.RemoteRoot.TrimEnd('/');
            var remoteChild = child.RemoteRoot.TrimEnd('/');
            var localParent = parent.LocalRoot.TrimEnd('/');
            var localChild = child.LocalRoot.TrimEnd('/');
            if (parent.Service != child.Service || remoteParent.Length == 0 || localParent.Length == 0 || localChild.Length == 0 ||
                !remoteChild.StartsWith(remoteParent + "/", StringComparison.Ordinal))
            {
                return false;
            }
        
            var expected = localParent + remoteChild[remoteParent.Length..];
            // A trusted child root can permit an alias that the parent mapping refuses. Keep it in that case.
            return expected == localChild && physicalPaths.Map(remoteChild, remoteParent, localParent) == localChild;
        }
        
        private async ValueTask<string> SuggestLocalAsync(MediaService service, string remote, int remoteCount, string[] localPaths, StreamingOptions config,
            CancellationToken cancellationToken)
        {
            var saved = config.PathMappings.FirstOrDefault(mapping => mapping.Service == service &&
                                                                      mapping.RemoteRoot.TrimEnd('/') == remote.TrimEnd('/'));
            if (saved is not null)
            {
                return saved.LocalRoot;
            }

            // A verified administrator mapping is stronger evidence than matching folder names.
            if (await mediator.Send(new MapServicePath.Query(service, remote), cancellationToken).ConfigureAwait(false) is { } mapped)
            {
                return mapped;
            }

            var matches = localPaths.Where(local => Path.GetFileName(local.TrimEnd('/')).Equals(Path.GetFileName(remote.TrimEnd('/')), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 1)
            {
                return matches[0];
            }

            // A lone movie/show root can be paired by library type. Multiple roots need a manual choice.
            return service != MediaService.Transmission && remoteCount == 1 && localPaths.Length == 1 ? localPaths[0] : "";
        }

        private static bool Within(string root, string path)
        {
            return !string.IsNullOrWhiteSpace(root) &&
                   (path == root.TrimEnd('/') || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal));
        }
    }
}
