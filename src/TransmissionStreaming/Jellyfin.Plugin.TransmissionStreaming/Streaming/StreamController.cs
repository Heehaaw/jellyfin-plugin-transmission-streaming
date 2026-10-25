namespace Jellyfin.Plugin.TransmissionStreaming.Streaming;

using Jellyfin.Plugin.TransmissionStreaming.Http;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Logic.Status;
using global::TransmissionStreaming.Logic.Streaming;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Adapts Jellyfin HTTP requests to the streaming application's use cases.</summary>
[ApiController]
[Route(StreamingConstants.Route)]
public sealed class StreamController(IMediator mediator, ILogger<StreamController> logger) : ControllerBase
{
    [HttpGet("status")]
    [Authorize(Policy = StreamingConstants.RequiresAdministrator)]
    public async Task<ActionResult<StreamingStatus>> Status(CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetStatus.Query(), cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("streams/{id}")]
    [HttpHead("streams/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Stream(string id, [FromQuery] string? key, CancellationToken cancellationToken)
    {
        var entry = await mediator.Send(new GetStream.Query(id, key), cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return NotFound();
        }

        if (!HttpByteRange.TryParse(Request.Headers.Range.ToString(), entry.Length, out var range))
        {
            Response.Headers.ContentRange = $"bytes */{entry.Length}";
            return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
        }

        Response.StatusCode = range.Partial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
        Response.ContentLength = range.Count;
        Response.ContentType = ContentType(entry.SourceName);
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.CacheControl = "private, no-store";
        if (range.Partial)
        {
            Response.Headers.ContentRange = $"bytes {range.Start}-{range.Start + range.Count - 1}/{entry.Length}";
        }

        // Scanners can read length without waking a torrent or waiting for its missing pieces.
        if (HttpMethods.IsHead(Request.Method) || range.Count == 0)
        {
            return new EmptyResult();
        }

        try
        {
            var command = new StreamMedia.Command(entry, Response.Body, range.Start, range.Count);
            await mediator.Send(command, cancellationToken).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or TimeoutException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogWarning("Transmission stream stopped ({FailureType}); missing bytes were not substituted.", exception.GetType().Name);
            if (Response.HasStarted)
            {
                // Once video bytes have been sent, an error document would corrupt the response.
                HttpContext.Abort();
                return new EmptyResult();
            }

            Response.ContentLength = null;
            Response.Headers.Remove("Content-Range");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string ContentType(string name)
    {
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            ".ts" => "video/mp2t",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            _ => "video/x-matroska"
        };
    }
}
