namespace TransmissionStreaming.Logic.Discovery;

using Core.Transmission;
using Mediator;

/// <summary>Selects eligible torrent payloads while identifying the primary movie.</summary>
public static class SelectVideoFiles
{
    /// <summary>Requests playable file indexes and the largest eligible movie in torrent order.</summary>
    public sealed record Query(IReadOnlyList<TorrentFileSnapshot> Files) : IQuery<Result>;

    /// <summary>Episode discovery can use every playable index; movie discovery uses only the primary index.</summary>
    public sealed record Result(IReadOnlyList<int> PlayableIndexes, int? PrimaryMovieIndex);

    internal sealed class Handler : IQueryHandler<Query, Result>
    {
        public ValueTask<Result> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var playable = new List<int>();
            int? primary = null;
            long largest = 0;
            for (var index = 0; index < query.Files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = query.Files[index];
                if (file.Length <= 0 || !MediaFileName.IsPlayable(file.Name))
                {
                    continue;
                }

                playable.Add(index);
                // Equal-sized candidates retain the torrent's original ordering.
                if (file.Length > largest)
                {
                    largest = file.Length;
                    primary = index;
                }
            }

            return ValueTask.FromResult(new Result(playable, primary));
        }

    }
}
