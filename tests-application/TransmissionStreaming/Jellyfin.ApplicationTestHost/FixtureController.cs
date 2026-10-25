namespace Jellyfin.ApplicationTestHost;

using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Discovery;

/// <summary>Clears scenario-owned projection rows inside the disposable Jellyfin process.</summary>
/// <remarks>
/// This controller is compiled into the application-test helper DLL, not either production plugin. The application test build copies it to
/// TestHost beside the runner; JellyfinContainer then adds it to the disposable Transmission plugin folder and that copy's assembly whitelist.
/// Loading it in the same plugin assembly context preserves the identity of the Core services injected here. See this project's README.md.
///
/// The live host owns pooled SQLite connections. Resetting rows through its own SQLite runtime avoids the inconsistent view observed
/// when a separate process on the macOS side of the Docker VM changed the bind-mounted database. The fixture removes Jellyfin libraries and viewers
/// through supported APIs; this controller only clears the plugin's two tables, after discovery is disabled and its active poll has finished.
/// </remarks>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("ApplicationTestHost")]
public sealed class FixtureController : ControllerBase
{
    private readonly IApplicationPaths paths;
    private readonly IOptionsMonitor<StreamingOptions> options;
    private readonly IDiscoveryStatus discovery;

    /// <summary>Uses the disposable server's paths and the production plugin's current configuration and discovery status.</summary>
    /// <param name="paths">Jellyfin's paths inside the container, where its SQLite runtime sees the live database.</param>
    /// <param name="options">The same live settings observed by the production discovery worker.</param>
    /// <param name="discovery">The production worker's status, used to reject a reset while discovery is still active.</param>
    public FixtureController(IApplicationPaths paths, IOptionsMonitor<StreamingOptions> options, IDiscoveryStatus discovery)
    {
        this.paths = paths;
        this.options = options;
        this.discovery = discovery;
    }

    /// <summary>Deletes active and completed projection rows in one transaction without replacing database files.</summary>
    /// <remarks>
    /// JellyfinHost first disables discovery, drains its poll and playback readers, and removes scenario libraries and viewers. This endpoint rechecks
    /// the discovery precondition before writing. It leaves schema, connections, files, media, and Jellyfin's own database intact. The fixture restores
    /// disabled defaults and verifies that the server is empty before releasing the scenario lease; a failed cleanup prevents further host reuse.
    /// </remarks>
    /// <param name="cancellationToken">Cancels database work if the fixture request or server shuts down.</param>
    /// <returns>No content after the transaction commits, or conflict while discovery is enabled or still polling.</returns>
    [HttpDelete("state")]
    public async Task<IActionResult> ResetAsync(CancellationToken cancellationToken)
    {
        if (options.CurrentValue.Enabled || discovery.Get().IsPolling)
        {
            return Conflict("Disable discovery before resetting a scenario.");
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(paths.DataPath, "transmission-streaming", "projections.db"),
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM projections; DELETE FROM completed_projections;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}
