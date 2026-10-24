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
            var localRoot = string.IsNullOrWhiteSpace(configuration.LocalMediaRoot)
                ? await mediator.Send(new InferMediaRoot.Query(libraries.GetPaths(), projectionRoot), cancellationToken).ConfigureAwait(false)
                : configuration.LocalMediaRoot;
            return new ConfigurationDefaults(projectionRoot, localRoot);
        }
    }
}
