namespace TransmissionStreaming.Logic.Discovery;

using TransmissionStreaming.Core.Transmission;

/// <summary>Selects playable payloads before discovery assigns a permanent movie identity.</summary>
internal interface IVideoFileSelection
{
    bool IsPlayable(string name);
    int? PrimaryMovieIndex(IReadOnlyList<TorrentFileSnapshot> files);
}
