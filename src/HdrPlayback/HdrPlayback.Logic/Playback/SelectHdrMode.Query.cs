namespace HdrPlayback.Logic.Playback;

using Core.Playback;
using Mediator;

/// <summary>Selects optional HDR conversion from source, client, user and server capabilities.</summary>
public static class SelectHdrMode
{
    /// <summary>Carries the facts for a single playback negotiation.</summary>
    public sealed record Query(HdrPlaybackContext Context) : IQuery<HdrPlaybackDecision>;

    internal sealed class Handler : IQueryHandler<Query, HdrPlaybackDecision>
    {
        public ValueTask<HdrPlaybackDecision> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = query.Context;
            var reason = UnavailableReason(source);
            return ValueTask.FromResult(new HdrPlaybackDecision(reason is null, source.Requested && reason is null, reason));
        }

        private static string? UnavailableReason(HdrPlaybackContext source)
        {
            if (!source.SourceIsSdr)
            {
                return "HDR conversion is only available for SDR video.";
            }

            if (!source.CanTranscode)
            {
                return "Video transcoding is disabled for this user.";
            }

            if (!source.ClientSupportsHdr)
            {
                return "This player does not advertise HEVC HDR10 playback.";
            }

            if (!source.PipelineSupported)
            {
                return "HDR conversion requires configured VA-API HEVC encoding and Vulkan/libplacebo support.";
            }

            return null;
        }
    }
}
