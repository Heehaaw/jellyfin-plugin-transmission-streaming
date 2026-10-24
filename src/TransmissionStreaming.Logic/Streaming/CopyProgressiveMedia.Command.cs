namespace TransmissionStreaming.Logic.Streaming;

using Core.Filesystem;
using Core.Streaming;
using Mediator;
using System.Buffers;
using ProjectionEntry = Core.Projection.Projection;

/// <summary>Copies verified media ranges without reading sparse gaps or depending on a physical filesystem.</summary>
public static class CopyProgressiveMedia
{
    /// <summary>Copies a fixed byte range to the caller-owned destination while waiting for missing verified pieces.</summary>
    /// <remarks>The destination remains open. Cancellation or failure may leave a partially written response.</remarks>
    public sealed record Command(ProjectionEntry Projection, Stream Destination, long Offset, long Count, ProgressiveReadOptions Options) : ICommand
    {
        public override string ToString()
        {
            return "CopyProgressiveMedia (stream details redacted)";
        }
    }

    internal sealed class Handler(IMediator mediator, IMediaFiles mediaFiles, TimeProvider timeProvider) : ICommandHandler<Command>
    {
        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var offset = command.Offset;
            var count = command.Count;
            var options = command.Options;
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (options.BufferSize is < 4096 or > 1024 * 1024 || options.PollInterval <= TimeSpan.Zero || options.StallTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            var playback = new PlaybackSession(command.Projection);
            var end = checked(offset + count);
            var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
            var stalledAt = timeProvider.GetTimestamp();
            var position = offset;
            long? prioritized = null;
            try
            {
                while (position < end)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var state = await mediator.Send(new GetPlaybackState.Query(playback), cancellationToken).ConfigureAwait(false);
                    if (end > state.Length)
                    {
                        throw new ArgumentOutOfRangeException(nameof(count), "Requested range exceeds the media length.");
                    }

                    if (!state.DiskWritesSafe)
                    {
                        throw new IOException("Progressive reads require Transmission sequential download or a disabled write cache.");
                    }

                    var requested = (int)Math.Min(end - position, options.BufferSize);
                    var available = state.Complete ? requested : PieceMap.AvailableBytes(state.Pieces, state.PieceSize, checked(state.FileOffset + position), requested);
                    var read = 0;
                    if (available > 0 && state.Path is not null)
                    {
                        read = await mediaFiles.ReadAsync(state.Path, buffer.AsMemory(0, available), position, cancellationToken).ConfigureAwait(false);
                    }

                    if (read > 0)
                    {
                        await command.Destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        position += read;
                        stalledAt = timeProvider.GetTimestamp();
                        prioritized = null;
                        continue;
                    }

                    if (options.PrioritizeMissingPieces && prioritized != position)
                    {
                        await mediator.Send(new PrioritizePlayback.Command(playback, position), cancellationToken).ConfigureAwait(false);
                        prioritized = position;
                    }

                    var stalledFor = timeProvider.GetElapsedTime(stalledAt);
                    if (stalledFor >= options.StallTimeout)
                    {
                        throw new TimeoutException("The requested media bytes did not become available before the playback stall deadline.");
                    }

                    // A long polling interval must not extend the final stall deadline.
                    var remaining = options.StallTimeout - stalledFor;
                    var delay = options.PollInterval < remaining ? options.PollInterval : remaining;
                    await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return Unit.Value;
        }
    }
}
