namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;

/// <summary>Merges Jellyfin user metadata without clearing newer or manual state.</summary>
internal interface IWatchStateMerger
{
    /// <summary>Combines a duplicate with the permanent entry, preserving manual or newer playback and personal metadata.</summary>
    UpdateUserItemDataDto Merge(UserItemData source, UserItemData? target);
}
