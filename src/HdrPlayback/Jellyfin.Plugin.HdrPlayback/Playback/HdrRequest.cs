namespace Jellyfin.Plugin.HdrPlayback.Playback;

using global::HdrPlayback.Core.Playback;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

internal static class HdrRequest
{
    public const string ModeHeader = "X-Jellyfin-Hdr-Conversion";
    public const string AvailableHeader = "X-Jellyfin-Hdr-Available";
    public const string ReasonHeader = "X-Jellyfin-Hdr-Reason";
    public const string NegotiationKey = "HdrPlayback.Negotiation";

    public static Guid UserId(ClaimsPrincipal user)
    {
        return Guid.TryParse(user.FindFirstValue("Jellyfin-UserId"), out var id) ? id : Guid.Empty;
    }

    public static HdrSessionKey Session(HttpContext context, string? playSessionId = null)
    {
        return new HdrSessionKey(
            UserId: UserId(context.User),
            DeviceId: context.User.FindFirstValue("Jellyfin-DeviceId") ?? "",
            PlaySessionId: playSessionId ?? context.Request.Query["PlaySessionId"].ToString()
        );
    }
}
