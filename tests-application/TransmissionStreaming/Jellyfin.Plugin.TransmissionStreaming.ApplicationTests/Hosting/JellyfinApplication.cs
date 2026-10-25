namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using global::TransmissionStreaming.TestSupport.Torrents;
using Ui;
using Xunit;

/// <summary>Owns all application-test infrastructure and shuts consumers down before their networks and files.</summary>
public sealed class JellyfinApplication : IAsyncLifetime
{
    internal DownloadServices Downloads { get; } = new();
    internal JellyfinBaseline Baseline { get; } = new();
    internal HeadlessBrowser Browser { get; } = new();
    internal JellyfinHost Host { get; } = new();

    /// <inheritdoc />
    public ValueTask InitializeAsync()
    {
        return Baseline.InitializeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var errors = new List<Exception>();
        // Explicit ownership avoids relying on xUnit's ordering of independent assembly fixtures.
        foreach (var resource in new IAsyncDisposable[] { Host, Browser, Downloads, Baseline })
        {
            try
            {
                await resource.DisposeAsync();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (errors.Count != 0)
        {
            throw new AggregateException("Application infrastructure cleanup failed.", errors);
        }
    }
}
