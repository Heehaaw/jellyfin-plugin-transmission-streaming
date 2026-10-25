namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Logic.Configuration;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Fills missing local settings once the host has composed its library services.</summary>
internal sealed class ConfigurationInitializer(IPluginSettings settings, IServiceScopeFactory scopeFactory, ILogger<ConfigurationInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PrefillAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Prefill is optional; a library that is not ready must not prevent Jellyfin from starting.
            var failureType = exception.GetType().Name;
            logger.LogWarning("Configuration prefill was skipped ({FailureType}); supplied settings were retained.", failureType);
        }
    }

    private async Task PrefillAsync(CancellationToken cancellationToken)
    {
        var configuration = settings.Configuration;
        if (!string.IsNullOrWhiteSpace(configuration.ProjectionRoot) && !string.IsNullOrWhiteSpace(configuration.LocalMediaRoot))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new GetConfigurationDefaults.Query();
        var defaults = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        ApplyDefaults(defaults);
    }

    private void ApplyDefaults(ConfigurationDefaults defaults)
    {
        // Read after discovery so a concurrent save keeps ownership of supplied values.
        var configuration = settings.Configuration;
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
