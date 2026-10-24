namespace TransmissionStreaming.Logic.Discovery;

using TransmissionStreaming.Core.Transmission;

/// <summary>Selects playable payloads before discovery assigns a permanent movie identity.</summary>
internal interface IVideoFileSelection
{
    /// <summary>Recognizes supported video filenames while excluding samples and auxiliary payloads.</summary>
    bool IsPlayable(string name);
    /// <summary>Selects the largest eligible movie payload in torrent order, returning null when none is playable.</summary>
    int? PrimaryMovieIndex(IReadOnlyList<TorrentFileSnapshot> files);
}
