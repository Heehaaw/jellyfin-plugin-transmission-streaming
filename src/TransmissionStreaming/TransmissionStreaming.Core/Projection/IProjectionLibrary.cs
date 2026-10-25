namespace TransmissionStreaming.Core.Projection;

/// <summary>Host indexing, item metadata, and per-user state operations without application policy.</summary>
public interface IProjectionLibrary
{
    /// <summary>Indexes host libraries containing the dedicated projection folder.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>Indexes configured host libraries containing an imported local path.</summary>
    Task IndexImportedAsync(string path, CancellationToken cancellationToken);

    /// <summary>Reads canonical item metadata and current playback activity for an exact local path.</summary>
    LibraryItem? Find(string path);

    /// <summary>Writes the item's display name and description without changing identity or user data.</summary>
    Task UpdateMetadataAsync(string itemId, string name, string? overview, CancellationToken cancellationToken);

    /// <summary>Loads hydrated source and destination watch data for every host user.</summary>
    IReadOnlyList<LibraryUserState> GetUserStates(string sourceItemId, string targetItemId);

    /// <summary>Saves one user's merged watch data to a host item.</summary>
    void SaveUserState(string userId, string itemId, LibraryWatchState state);

    /// <summary>Removes only the host database entry; media files are never deleted and missing entries are unchanged.</summary>
    void Remove(string itemId);
}
