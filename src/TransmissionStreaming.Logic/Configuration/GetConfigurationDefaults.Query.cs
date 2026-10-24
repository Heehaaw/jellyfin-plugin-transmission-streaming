namespace TransmissionStreaming.Logic.Configuration;

using Mediator;
using TransmissionStreaming.Core.Configuration;

/// <summary>Defines the query for safe configuration suggestions from existing local libraries.</summary>
public static class GetConfigurationDefaults
{
    /// <summary>Requests path defaults without inferring remote services or credentials.</summary>
    public sealed record Query : IQuery<ConfigurationDefaults>;

    internal sealed class Handler(IStreamingOptionsProvider options, IMediaLibraryLocations libraries, IMediaRootInference mediaRoots)
        : IQueryHandler<Query, ConfigurationDefaults>
    {
        public ValueTask<ConfigurationDefaults> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = options.Get();
            var projectionRoot = string.IsNullOrWhiteSpace(configuration.ProjectionRoot)
                ? Path.Combine(configuration.StateDirectory, "library")
                : configuration.ProjectionRoot;
            var localRoot = string.IsNullOrWhiteSpace(configuration.LocalMediaRoot)
                ? mediaRoots.Infer(libraries.GetPaths(), projectionRoot)
                : configuration.LocalMediaRoot;
            return ValueTask.FromResult(new ConfigurationDefaults(projectionRoot, localRoot));
        }
    }
}
