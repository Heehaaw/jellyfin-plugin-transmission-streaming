namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Xunit;

/// <summary>Amortizes migrations and initial setup while giving every scenario an independent copy of an empty server database.</summary>
public sealed class JellyfinBaseline : IAsyncLifetime
{
    private readonly string workspace = Path.Combine(
        path1: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        path2: ".cache",
        path3: "transmission-streaming-tests",
        path4: "baseline-" + Guid.NewGuid().ToString("N")
    );

    private readonly INetwork network = new NetworkBuilder().Build();
    private string Configuration => Path.Combine(workspace, "jellyfin");

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(workspace);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await network.CreateAsync(deadline.Token);
            await using (var server = new JellyfinContainer(workspace, network, false))
            {
                await server.StartAsync(deadline.Token);
                using var api = new JellyfinApi(server.Address);
                await api.AuthenticateAsync();
                await server.StopAsync(deadline.Token);
            }
            // Graceful stop flushes SQLite before this tree becomes a template.
            // No media libraries, plugin, or download connections have been configured here.
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>Copies the stopped baseline into a new configuration directory; mutable files are never shared between scenarios.</summary>
    public void Populate(string destination)
    {
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            throw new InvalidOperationException("A baseline must populate an empty scenario configuration directory.");
        }

        CopyDirectory(Configuration, destination);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await network.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
