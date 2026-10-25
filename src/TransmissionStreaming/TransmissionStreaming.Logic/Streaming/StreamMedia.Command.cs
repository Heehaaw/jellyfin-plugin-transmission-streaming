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
        IStreamActivity activity
    ) : ICommandHandler<Command>
    {
        private const int MinimumStallSeconds = 5;
        private const int MaximumStallSeconds = 600;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var configuration = options.CurrentValue;
            if (!configuration.Enabled)
            {
                throw new InvalidOperationException("Transmission streaming is disabled.");
            }

            var entry = command.Projection;
            if (entry.CompletedPath is null && configuration.PrioritizePlayback)
            {
                var connection = new TransmissionConnection(configuration.TransmissionUrl, configuration.TransmissionUsername, configuration.TransmissionPassword);
                await transmission.EnableSequentialAsync(connection, entry.TorrentHash, cancellationToken).ConfigureAwait(false);
            }

            var stallSeconds = Math.Clamp(configuration.PieceWaitSeconds, MinimumStallSeconds, MaximumStallSeconds);
            var settings = new ProgressiveReadOptions
            {
                StallTimeout = TimeSpan.FromSeconds(stallSeconds),
                PrioritizeMissingPieces = configuration.PrioritizePlayback
            };
            using var lease = activity.Open(entry.Id);
            var copy = new CopyProgressiveMedia.Command(entry, command.Destination, command.Offset, command.Count, settings);
            await mediator.Send(copy, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
