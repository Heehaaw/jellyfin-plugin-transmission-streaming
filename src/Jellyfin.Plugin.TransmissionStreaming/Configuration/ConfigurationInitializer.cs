namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Logic.Configuration;
using Mediator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Fills missing local settings once the host has composed its library services.</summary>
internal sealed class ConfigurationInitializer(IPluginSettings settings, IMediator mediator, ILogger<ConfigurationInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configuration = settings.Configuration;
            if (!string.IsNullOrWhiteSpace(configuration.ProjectionRoot) && !string.IsNullOrWhiteSpace(configuration.LocalMediaRoot))
            {
                return;
            }

            var defaults = await mediator.Send(new GetConfigurationDefaults.Query(), cancellationToken).ConfigureAwait(false);
            // Read again after discovery so a concurrent settings save keeps ownership of supplied values.
            configuration = settings.Configuration;
            var changed = false;
            if (string.IsNullOrWhiteSpace(configuration.ProjectionRoot))
            {
                configuration.ProjectionRoot = defaults.ProjectionRoot;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(configuration.LocalMediaRoot) && !string.IsNullOrWhiteSpace(defaults.LocalMediaRoot))
            {
                configuration.LocalMediaRoot = defaults.LocalMediaRoot;
                changed = true;
            }
            if (changed)
            {
                settings.UpdateConfiguration(configuration);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Prefill is optional; a library that is not ready must not prevent Jellyfin from starting.
            logger.LogWarning("Configuration prefill was skipped ({FailureType}); supplied settings were retained.", exception.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
