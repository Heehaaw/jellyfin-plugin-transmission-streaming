namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Logic.Configuration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Streaming;
using System.Text.Json;

/// <summary>Provides administrator-only local suggestions without changing saved configuration.</summary>
[ApiController]
[Route(StreamingConstants.Route + "/configuration")]
[Authorize(Policy = StreamingConstants.RequiresAdministrator)]
public sealed class ConfigurationController(IMediator mediator) : ControllerBase
{
    /// <summary>Discovers paths with the form's connection details; suggestions are saved only by the settings page.</summary>
    [HttpPost("paths/{service}")]
    public async Task<ActionResult<ServicePathSuggestions>> Paths(MediaService service, [FromBody] ServiceConnection connection, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(service))
        {
            return BadRequest("Unknown media service.");
        }

        try
        {
            return await mediator.Send(new DiscoverServicePaths.Query(service, connection), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or ArgumentException)
        {
            // Upstream exception messages can contain a URL or credentials. Return only a safe, actionable failure.
            return Problem("Could not discover paths. Check the service URL, credentials and connectivity.", statusCode: 502);
        }
    }

    /// <summary>Returns local defaults inferred from this host and its existing media libraries.</summary>
    [HttpGet("defaults")]
    public async Task<ActionResult<ConfigurationDefaults>> Defaults(CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetConfigurationDefaults.Query(), cancellationToken).ConfigureAwait(false);
    }
}
