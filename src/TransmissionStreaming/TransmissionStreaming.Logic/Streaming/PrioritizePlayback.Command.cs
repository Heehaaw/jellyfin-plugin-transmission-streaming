namespace TransmissionStreaming.Logic.Streaming;

using Core.Configuration;
using Core.Streaming;
using Core.Transmission;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Moves Transmission's sequential cursor toward the missing bytes of one playback session.</summary>
public static class PrioritizePlayback
{
    /// <summary>Prioritizes a file-relative offset when cached torrent metadata supports sequential playback.</summary>
    public sealed record Command(PlaybackSession Playback, long FileOffset) : ICommand;

    internal sealed class Handler(ITransmissionClient client, IOptionsMonitor<StreamingOptions> options) : ICommandHandler<Command>
    {
        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = command.Playback.Entry;
            if (command.Playback.Torrent is not { SequentialDownload: true, PieceSize: > 0 } torrent || entry.FileIndex < 0 || entry.FileIndex >= torrent.Files.Count)
            {
                return Unit.Value;
            }

            var piece = checked((int)((torrent.GetFileOffset(entry.FileIndex) + command.FileOffset) / torrent.PieceSize));
            var config = options.CurrentValue;
            var connection = new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword);
            await client.SetSequentialStartAsync(connection, entry.TorrentHash, piece, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }
}
