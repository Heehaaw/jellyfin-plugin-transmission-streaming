namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Logic.Configuration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Streaming;

/// <summary>Provides administrator-only local suggestions without changing saved configuration.</summary>
[ApiController]
[Route(StreamingConstants.Route + "/configuration")]
[Authorize(Policy = StreamingConstants.RequiresAdministrator)]
public sealed class ConfigurationController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns local defaults inferred from this host and its existing media libraries.</summary>
    [HttpGet("defaults")]
    public async Task<ActionResult<ConfigurationDefaults>> Defaults(CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetConfigurationDefaults.Query(), cancellationToken).ConfigureAwait(false);
    }
}
