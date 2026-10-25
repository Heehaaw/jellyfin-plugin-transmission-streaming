namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

/// <summary>Invalidates framework options after Jellyfin persists plugin settings.</summary>
internal sealed class PluginOptionsChangeTokenSource : IOptionsChangeTokenSource<StreamingOptions>, IDisposable
{
    private readonly object gate = new();
    private readonly IPluginSettings settings;
    private CancellationTokenSource changed = new();
    private bool disposed;

    public PluginOptionsChangeTokenSource(IPluginSettings settings)
    {
        this.settings = settings;
        settings.ConfigurationChanged += OnConfigurationChanged;
    }

    public string Name => Options.DefaultName;

    public IChangeToken GetChangeToken()
    {
        lock (gate)
        {
            return new CancellationChangeToken(disposed ? CancellationToken.None : changed.Token);
        }
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration configuration)
    {
        CancellationTokenSource previous;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            previous = changed;
            changed = new CancellationTokenSource();
        }

        // Rotate first: OptionsMonitor re-subscribes synchronously when cancellation notifies it.
        // Invoke callbacks outside the lock, since they rebuild options from the host configuration.
        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource previous;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            previous = changed;
        }

        settings.ConfigurationChanged -= OnConfigurationChanged;
        // Shutdown does not represent a settings change and must not rebuild options.
        previous.Dispose();
    }
}
