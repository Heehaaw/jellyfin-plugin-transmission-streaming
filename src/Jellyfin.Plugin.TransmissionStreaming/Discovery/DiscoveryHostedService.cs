namespace Jellyfin.Plugin.TransmissionStreaming.Discovery;

using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Discovery;
using global::TransmissionStreaming.Logic.Discovery;
using Mediator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Drives the discovery use case from Jellyfin's hosted-service lifecycle.</summary>
internal sealed class DiscoveryHostedService(
    IMediator mediator,
    IStreamingOptionsProvider configuration,
    IDiscoveryStatus status,
    ILogger<DiscoveryHostedService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (configuration.Get().Enabled)
            {
                try
                {
                    await mediator.Send(new DiscoverDownloads.Command(), stoppingToken).ConfigureAwait(false);
                    status.Set(new DiscoveryStatus { LastSuccessfulPoll = timeProvider.GetUtcNow() });
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    // HTTP exception messages may include upstream URLs. Keep them out of logs and status.
                    var failure = exception.GetType().Name;
                    status.Set(status.Get() with { LastFailure = failure });
                    logger.LogWarning("Transmission discovery failed ({FailureType}). Check connections and paths.", failure);
                }
            }
            var delay = Math.Clamp(configuration.Get().PollSeconds, DiscoveryConstants.MinimumPollSeconds, DiscoveryConstants.MaximumPollSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delay), timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }
}
