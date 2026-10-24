namespace TransmissionStreaming.Logic.Streaming;

using Mediator;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Streaming;
using TransmissionStreaming.Core.Transmission;

/// <summary>Defines the StreamMedia command and its application handler.</summary>
public static class StreamMedia
{
    /// <summary>Copies an authorized projection's requested byte range to an output stream.</summary>
    public sealed record Command(Projection Projection, Stream Destination, long Offset, long Count) : ICommand
    {
        public override string ToString()
        {
            return "StreamMedia (stream details redacted)";
        }
    }

    /// <summary>Coordinates bounded playback while leaving HTTP and file access to adapters.</summary>
    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        ITransmissionClient transmission,
        IMediator mediator,
        IStreamActivity activity) : ICommandHandler<Command>
    {
        private const int MinimumStallSeconds = 5;
        private const int MaximumStallSeconds = 600;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var config = options.CurrentValue;
            if (!config.Enabled)
            {
                throw new InvalidOperationException("Transmission streaming is disabled.");
            }

            var entry = command.Projection;
            if (entry.CompletedPath is null && config.PrioritizePlayback)
            {
                var connection = new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword);
                await transmission.EnableSequentialAsync(connection, entry.TorrentHash, cancellationToken).ConfigureAwait(false);
            }

            var settings = new ProgressiveReadOptions
            {
                StallTimeout = TimeSpan.FromSeconds(Math.Clamp(config.PieceWaitSeconds, MinimumStallSeconds, MaximumStallSeconds)),
                PrioritizeMissingPieces = config.PrioritizePlayback
            };
            using var lease = activity.Open(entry.Id);
            await mediator.Send(new CopyProgressiveMedia.Command(entry, command.Destination, command.Offset, command.Count, settings), cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
