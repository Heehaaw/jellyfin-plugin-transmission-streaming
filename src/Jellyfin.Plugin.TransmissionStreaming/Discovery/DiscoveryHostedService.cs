namespace Jellyfin.Plugin.TransmissionStreaming.Discovery;

using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Discovery;
using global::TransmissionStreaming.Logic.Discovery;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Drives the discovery use case from Jellyfin's hosted-service lifecycle.</summary>
internal sealed class DiscoveryHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<StreamingOptions> configuration,
    IDiscoveryStatus status,
    ILogger<DiscoveryHostedService> logger,
    TimeProvider timeProvider
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PollAsync(stoppingToken).ConfigureAwait(false);

                var intervalSeconds = Math.Clamp(configuration.CurrentValue.PollSeconds, DiscoveryConstants.MinimumPollSeconds, DiscoveryConstants.MaximumPollSeconds);
                var interval = TimeSpan.FromSeconds(intervalSeconds);
                await Task.Delay(interval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown cancels both an in-flight poll and its next scheduled delay.
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        if (!configuration.CurrentValue.Enabled)
        {
            return;
        }

        try
        {
            // A hosted worker outlives requests; each poll owns and disposes its application scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var command = new DiscoverDownloads.Command();
            await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            var successfulPoll = new DiscoveryStatus
            {
                LastSuccessfulPoll = timeProvider.GetUtcNow()
            };
            status.Set(successfulPoll);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // HTTP exception messages may include upstream URLs. Keep them out of logs and status.
            var failureType = exception.GetType().Name;
            var failedPoll = status.Get() with
            {
                LastFailure = failureType
            };
            status.Set(failedPoll);
            logger.LogWarning("Transmission discovery failed ({FailureType}). Check connections and paths.", failureType);
        }
    }
}
