namespace Jellyfin.Plugin.TransmissionStreaming.Streaming;

using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Core.Transmission;
using Jellyfin.Plugin.TransmissionStreaming;
using Jellyfin.Plugin.TransmissionStreaming.Discovery;
using Jellyfin.Plugin.TransmissionStreaming.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;


/// <summary>Serves a stable capability URL from a piece-gated or completed local source.</summary>
[ApiController]
[Route(StreamingConstants.Route)]
public sealed class StreamController(
    Plugin plugin,
    ProjectionStore store,
    TransmissionClient transmission,
    DiscoveryWorker discovery,
    PlaybackSourceFactory sources,
    ProgressiveReader reader,
    StreamActivity activity,
    ILogger<StreamController> logger) : ControllerBase
{
    [HttpGet("status")]
    [Authorize(Policy = StreamingConstants.RequiresAdministrator)]
    public ActionResult<object> Status() => new
    {
        enabled = plugin.Configuration.Enabled,
        discovery.LastSuccessfulPoll,
        discovery.LastFailure,
        entries = store.All().Select(entry => new
        {
            entry.Id,
            entry.Identity.Title,
            entry.Status,
            entry.Progress,
            readers = activity.Count(entry.Id)
        })
    };

    [HttpGet("streams/{id}")]
    [HttpHead("streams/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Stream(string id, [FromQuery] string? key, CancellationToken cancellationToken)
    {
        var entry = store.Get(id);
        if (!plugin.Configuration.Enabled || entry is null || !ValidKey(entry.Secret, key)) return NotFound();
        if (!ByteRange.TryParse(Request.Headers.Range.ToString(), entry.Length, out var range))
        {
            Response.Headers.ContentRange = $"bytes */{entry.Length}";
            return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
        }

        Response.StatusCode = range.Partial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
        Response.ContentLength = range.Count;
        Response.ContentType = ContentType(entry.SourceName);
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.CacheControl = "private, no-store";
        if (range.Partial) Response.Headers.ContentRange = $"bytes {range.Start}-{range.Start + range.Count - 1}/{entry.Length}";
        // Scanners can determine length without waking a torrent or waiting for video pieces.
        if (HttpMethods.IsHead(Request.Method) || range.Count == 0) return new EmptyResult();

        var config = plugin.Configuration;
        var connection = new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword);
        try
        {
            if (entry.CompletedPath is null && config.PrioritizePlayback)
                await transmission.EnableSequentialAsync(connection, entry.TorrentHash, cancellationToken).ConfigureAwait(false);
            var source = sources.Create(entry);
            var options = new ProgressiveReadOptions
            {
                StallTimeout = TimeSpan.FromSeconds(Math.Clamp(config.PieceWaitSeconds, StreamingConstants.MinimumStallSeconds, StreamingConstants.MaximumStallSeconds)),
                PrioritizeMissingPieces = config.PrioritizePlayback
            };
            using var lease = activity.Open(id);
            await reader.CopyAsync(source, Response.Body, range.Start, range.Count, options, cancellationToken).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception exception) when (exception is IOException or HttpRequestException or TimeoutException or InvalidOperationException)
        {
            logger.LogWarning("Transmission stream stopped ({FailureType}); no missing bytes were substituted.", exception.GetType().Name);
            if (Response.HasStarted)
            {
                // A partial successful response cannot be replaced with an error document mid-video.
                HttpContext.Abort();
                return new EmptyResult();
            }
            Response.ContentLength = null;
            Response.Headers.Remove("Content-Range");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool ValidKey(string expected, string? actual) => actual is not null && actual.Length == expected.Length
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".webm" => "video/webm",
        ".ts" => "video/mp2t",
        ".avi" => "video/x-msvideo",
        ".mov" => "video/quicktime",
        _ => "video/x-matroska"
    };
}
