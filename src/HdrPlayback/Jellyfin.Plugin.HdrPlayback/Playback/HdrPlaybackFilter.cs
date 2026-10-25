namespace Jellyfin.Plugin.HdrPlayback.Playback;

using global::HdrPlayback.Core.Playback;
using global::HdrPlayback.Logic.Playback;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

internal sealed class HdrPlaybackFilter(
    ILibraryManager library,
    IUserManager users,
    IMediaSourceManager sources,
    [FromKeyedServices(typeof(SelectHdrMode))] ISender sender,
    HdrCapabilities capabilities,
    IHdrSessionStore sessions
) : IAsyncResourceFilter, IAsyncResultFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsPost(request.Method) && request.Path.Value!.EndsWith("/Sessions/Playing/Stopped", StringComparison.OrdinalIgnoreCase))
        {
            var stop = await ReadBodyAsync(request).ConfigureAwait(false);
            if (stop is not null && Read<string>(stop, "PlaySessionId") is { } stoppedSession)
            {
                sessions.Remove(HdrRequest.Session(context.HttpContext, stoppedSession));
            }
        }

        if (!HttpMethods.IsPost(request.Method) || !request.Path.Value!.EndsWith("/PlaybackInfo", StringComparison.OrdinalIgnoreCase))
        {
            await next().ConfigureAwait(false);
            return;
        }

        if (!Guid.TryParse(context.RouteData.Values.GetValueOrDefault("itemId")?.ToString(), out var itemId))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var body = await ReadBodyAsync(request).ConfigureAwait(false);
        if (body is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var userId = HdrRequest.UserId(context.HttpContext.User);
        var user = userId == Guid.Empty ? null : users.GetUserById(userId);
        var item = user is null ? null : library.GetItemById<BaseItem>(itemId, user);
        if (item is null || item.MediaType != MediaType.Video)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var sourceId = Read<string>(body, "MediaSourceId") ?? item.Id.ToString("N");
        var source = await sources.GetMediaSource(item, sourceId, Read<string>(body, "LiveStreamId"), true, request.HttpContext.RequestAborted).ConfigureAwait(false);
        var profile = Get(body, "DeviceProfile")?.Deserialize<DeviceProfile>(JsonOptions);
        var requested = request.Headers[HdrRequest.ModeHeader] == "true";
        var facts = new HdrPlaybackContext(
            Requested: requested,
            SourceIsSdr: source?.VideoStream?.VideoRange == VideoRange.SDR,
            ClientSupportsHdr: HdrCapabilities.ClientSupportsHdr(profile),
            CanTranscode: user!.HasPermission(PermissionKind.EnableVideoPlaybackTranscoding) && Read<bool?>(body, "EnableTranscoding") != false,
            PipelineSupported: capabilities.PipelineSupported()
        );
        var decision = await sender.Send(new SelectHdrMode.Query(facts), request.HttpContext.RequestAborted).ConfigureAwait(false);
        context.HttpContext.Items[HdrRequest.NegotiationKey] = decision;
        if (requested && !decision.Available)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Title = "HDR conversion unavailable",
                Detail = decision.Reason
            });
            return;
        }

        if (!decision.Enabled)
        {
            await next().ConfigureAwait(false);
            return;
        }

        PrepareHdrProfile(body, profile!);
        // Rewrite before model binding, retaining native source, track, start-position and quality fields.
        var original = request.Body;
        var originalLength = request.ContentLength;
        await using var replaced = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(body));
        request.Body = replaced;
        request.ContentLength = replaced.Length;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            request.Body = original;
            request.ContentLength = originalLength;
        }
    }

    private static async Task<JsonObject?> ReadBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        try
        {
            return await JsonNode.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted).ConfigureAwait(false) as JsonObject;
        }
        catch (JsonException)
        {
            // Leave empty or malformed requests to Jellyfin's model binding and its normal validation response.
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static void PrepareHdrProfile(JsonObject body, DeviceProfile profile)
    {
        Set(body, "EnableDirectPlay", false);
        Set(body, "EnableDirectStream", false);
        Set(body, "AllowVideoStreamCopy", false);
        foreach (var transcode in profile.TranscodingProfiles.Where(candidate => candidate.Type == DlnaProfileType.Video))
        {
            transcode.VideoCodec = "hevc";
            transcode.Container = "mp4";
            transcode.Protocol = MediaStreamProtocol.hls;
        }

        Set(body, "DeviceProfile", JsonSerializer.SerializeToNode(profile, JsonOptions));
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.HttpContext.Items[HdrRequest.NegotiationKey] is HdrPlaybackDecision decision)
        {
            context.HttpContext.Response.Headers[HdrRequest.AvailableHeader] = decision.Available ? "true" : "false";
            context.HttpContext.Response.Headers[HdrRequest.ReasonHeader] = decision.Reason ?? "";
            if (decision.Enabled && context.Result is ObjectResult { Value: PlaybackInfoResponse playback } && playback.ErrorCode is null)
            {
                foreach (var source in playback.MediaSources.Where(source => !string.IsNullOrEmpty(source.TranscodingUrl)))
                {
                    source.SupportsDirectPlay = false;
                    source.SupportsDirectStream = false;
                    source.SupportsTranscoding = true;
                    source.TranscodingUrl = HdrTranscodingUrl(source.TranscodingUrl!);
                }

                sessions.Enable(HdrRequest.Session(context.HttpContext, playback.PlaySessionId));
            }
        }
        else if (context.Result is FileContentResult playlist
                 && playlist.ContentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase)
                 && sessions.Contains(HdrRequest.Session(context.HttpContext)))
        {
            var content = Encoding.UTF8.GetString(playlist.FileContents);
            if (content.Contains("#EXT-X-STREAM-INF", StringComparison.Ordinal))
            {
                // Native HLS returns UTF-8 file bytes and labels every encoded variant SDR.
                var converted = content.Replace("VIDEO-RANGE=SDR", "VIDEO-RANGE=PQ", StringComparison.Ordinal);
                playlist.FileContents = Encoding.UTF8.GetBytes(converted);
            }
        }

        await next().ConfigureAwait(false);
    }

    internal static string HdrTranscodingUrl(string url)
    {
        var split = url.IndexOf('?');
        var path = split < 0 ? url : url[..split];
        var query = QueryHelpers.ParseQuery(split < 0 ? "" : url[(split + 1)..]);
        query["VideoCodec"] = "hevc";
        query["Profile"] = "main10";
        query["MaxVideoBitDepth"] = "10";
        query["VideoRangeType"] = "HDR10";
        query["AllowVideoStreamCopy"] = "false";
        return QueryHelpers.AddQueryString(path, query);
    }

    private static JsonNode? Get(JsonObject body, string name)
    {
        return body.FirstOrDefault(property => property.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static T? Read<T>(JsonObject body, string name)
    {
        return Get(body, name) is { } value ? value.Deserialize<T>(JsonOptions) : default;
    }

    private static void Set(JsonObject body, string name, JsonNode? value)
    {
        var key = body.FirstOrDefault(property => property.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Key ?? name;
        body[key] = value;
    }
}
