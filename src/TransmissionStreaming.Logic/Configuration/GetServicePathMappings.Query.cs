namespace TransmissionStreaming.Logic.Configuration;

using Core.Configuration;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Selects the configured root pairs for one service.</summary>
public static class GetServicePathMappings
{
    /// <summary>Requests explicit mappings, with the shared legacy pair used only when none exist for this service.</summary>
    public sealed record Query(MediaService Service) : IQuery<IReadOnlyList<ServicePathMapping>>;

    internal sealed class Handler(IOptionsMonitor<StreamingOptions> options) : IQueryHandler<Query, IReadOnlyList<ServicePathMapping>>
    {
        public ValueTask<IReadOnlyList<ServicePathMapping>> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var config = options.CurrentValue;
            var mappings = config.PathMappings.Where(mapping => mapping.Service == query.Service).ToArray();
            // Incomplete explicit rows still replace the fallback; a typo must not select an unrelated old file.
            IReadOnlyList<ServicePathMapping> result = mappings.Length > 0
                ? mappings
                : [new ServicePathMapping { Service = query.Service, RemoteRoot = config.RemoteMediaRoot, LocalRoot = config.LocalMediaRoot }];
            return ValueTask.FromResult(result);
        }
    }
}
