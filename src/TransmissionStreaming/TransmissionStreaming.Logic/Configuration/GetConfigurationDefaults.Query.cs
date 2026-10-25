namespace TransmissionStreaming.Logic.Configuration;

using Mediator;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Configuration;

/// <summary>Defines the query for safe configuration suggestions from existing local libraries.</summary>
public static class GetConfigurationDefaults
{
    /// <summary>Requests path defaults without inferring remote services or credentials.</summary>
    public sealed record Query : IQuery<ConfigurationDefaults>;

    internal sealed class Handler(IOptionsMonitor<StreamingOptions> options, IMediaLibraryLocations libraries, IMediator mediator)
        : IQueryHandler<Query, ConfigurationDefaults>
    {
        public async ValueTask<ConfigurationDefaults> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = options.CurrentValue;
            var projectionRoot = string.IsNullOrWhiteSpace(configuration.ProjectionRoot)
                ? Path.Combine(configuration.StateDirectory, "library")
                : configuration.ProjectionRoot;
            var localRoot = configuration.LocalMediaRoot;
            if (string.IsNullOrWhiteSpace(localRoot))
            {
                var libraryPaths = libraries.GetPaths();
                var inferRoot = new InferMediaRoot.Query(libraryPaths, projectionRoot);
                localRoot = await mediator.Send(inferRoot, cancellationToken).ConfigureAwait(false);
            }

            return new ConfigurationDefaults(projectionRoot, localRoot);
        }
    }
}
