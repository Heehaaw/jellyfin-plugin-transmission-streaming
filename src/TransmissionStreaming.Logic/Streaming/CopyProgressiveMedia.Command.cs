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
            try
            {
                await CopyRangeAsync(command, playback, buffer, end, cancellationToken).ConfigureAwait(false);
            }

            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return Unit.Value;
        }

        private async Task CopyRangeAsync(Command command, PlaybackSession playback, byte[] buffer, long end, CancellationToken cancellationToken)
        {
            var position = command.Offset;
            var options = command.Options;
            var stalledAt = timeProvider.GetTimestamp();
            long? prioritizedPosition = null;
            while (position < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stateQuery = new GetPlaybackState.Query(playback);
                var state = await mediator.Send(stateQuery, cancellationToken).ConfigureAwait(false);
                var remainingBytes = end - position;
                var available = AvailableBytes(state, position, remainingBytes, options.BufferSize);
                var read = await CopyAvailableAsync(state.Path, command.Destination, buffer, position, available, cancellationToken).ConfigureAwait(false);
                if (read > 0)
                {
                    position += read;
                    stalledAt = timeProvider.GetTimestamp();
                    prioritizedPosition = null;
                    continue;
                }

                if (options.PrioritizeMissingPieces && prioritizedPosition != position)
                {
                    var prioritize = new PrioritizePlayback.Command(playback, position);
                    await mediator.Send(prioritize, cancellationToken).ConfigureAwait(false);
                    prioritizedPosition = position;
                }

                await WaitForPiecesAsync(stalledAt, options, cancellationToken).ConfigureAwait(false);
            }
        }

        private static int AvailableBytes(ProgressiveReadState state, long offset, long count, int bufferSize)
        {
            var end = checked(offset + count);
            if (end > state.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Requested range exceeds the media length.");
            }

            if (!state.DiskWritesSafe)
            {
                throw new IOException("Progressive reads require Transmission sequential download or a disabled write cache.");
            }

            var requested = (int)Math.Min(count, bufferSize);
            if (state.Complete)
            {
                return requested;
            }

            var torrentOffset = checked(state.FileOffset + offset);
            return PieceMap.AvailableBytes(state.Pieces, state.PieceSize, torrentOffset, requested);
        }

        private async Task<int> CopyAvailableAsync(string? path, Stream destination, byte[] buffer, long position, int available, CancellationToken cancellationToken)
        {
            if (available <= 0 || path is null)
            {
                return 0;
            }

            var availableBuffer = buffer.AsMemory(0, available);
            var read = await mediaFiles.ReadAsync(path, availableBuffer, position, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                return read;
            }

            var filledBuffer = buffer.AsMemory(0, read);
            await destination.WriteAsync(filledBuffer, cancellationToken).ConfigureAwait(false);
            return read;
        }

        private async Task WaitForPiecesAsync(long stalledAt, ProgressiveReadOptions options, CancellationToken cancellationToken)
        {
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
}
