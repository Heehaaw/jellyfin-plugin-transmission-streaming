namespace Jellyfin.Plugin.TransmissionStreaming.Library;

using global::TransmissionStreaming.Arr;
using Jellyfin.Plugin.TransmissionStreaming;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using System.Text;
using System.Xml.Linq;


/// <summary>Maintains permanent stream files and merges an import into their existing Jellyfin identity.</summary>
public sealed class LibraryProjection(
    Plugin plugin,
    ILibraryManager library,
    IUserManager users,
    IUserDataManager userData,
    ISessionManager sessions,
    WatchStateMerger watchStates)
{
    public string Root => string.IsNullOrWhiteSpace(plugin.Configuration.ProjectionRoot)
        ? Path.Combine(plugin.StateDirectory, "library")
        : Path.GetFullPath(plugin.Configuration.ProjectionRoot);

    public void WriteFiles(Projection entry)
    {
        var config = plugin.Configuration;
        var baseUri = new Uri(config.JellyfinPublicUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (baseUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(baseUri.UserInfo))
            throw new InvalidOperationException("Jellyfin URL must be HTTP(S), without embedded credentials.");
        var url = new Uri(baseUri, $"TransmissionStreaming/streams/{entry.Id}?key={entry.Secret}").AbsoluteUri;

        Directory.CreateDirectory(Path.GetDirectoryName(entry.StreamPath)!);
        WriteIfChanged(entry.StreamPath, url + "\n");
        var identity = entry.Identity;
        var episode = identity.Kind == MediaKind.Episode;
        var nfo = new XElement(episode ? "episodedetails" : "movie",
            new XElement("title", episode && !string.IsNullOrWhiteSpace(identity.EpisodeTitle) ? identity.EpisodeTitle : identity.Title),
            new XElement("year", identity.Year));
        if (episode)
        {
            nfo.Add(new XElement("showtitle", identity.Title), new XElement("season", identity.SeasonNumber), new XElement("episode", identity.EpisodeNumber));
            var seriesNfo = new XElement("tvshow", new XElement("title", identity.Title), new XElement("year", identity.Year));
            AddIds(seriesNfo, identity);
            WriteIfChanged(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(entry.StreamPath)!)!, "tvshow.nfo"), seriesNfo.ToString());
        }
        else AddIds(nfo, identity);
        WriteIfChanged(Path.ChangeExtension(entry.StreamPath, ".nfo"), nfo.ToString());
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Only libraries explicitly configured to contain the projection are refreshed.
        // A missing library leaves the files ready for the administrator to add later.
        var root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var folder in library.GetVirtualFolders(false))
        {
            if (!folder.Locations.Any(location => IsWithin(root, location) || IsWithin(location, root))) continue;
            if (Guid.TryParse(folder.ItemId, out var id) && library.GetItemById(id) is Folder item)
                await item.ValidateChildren(new Progress<double>(), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ReconcileAsync(Projection entry, CancellationToken cancellationToken)
    {
        var projected = Find(entry.StreamPath);
        if (projected is null) return;
        var identity = entry.Identity;
        var title = identity.Kind == MediaKind.Episode && !string.IsNullOrWhiteSpace(identity.EpisodeTitle)
            ? identity.EpisodeTitle : identity.Title;
        var display = plugin.Configuration.ShowDownloadStatus && entry.CompletedPath is null
            ? $"{title} [{entry.Status}]" : title;
        if (projected.Name != display && projected.GetParent() is { } parent)
        {
            projected.Name = display;
            await library.UpdateItemAsync(projected, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        if (entry.CompletedPath is null) return;
        var native = Find(entry.CompletedPath);
        if (native is null || native.Id == projected.Id || IsPlaying(native.Id)) return;

        // An import can be indexed before the next poll. Keep its history, then remove only
        // that duplicate database entry. The actual imported media is never deleted.
        foreach (var user in users.GetUsers())
        {
            var source = userData.GetUserData(user, native);
            var target = userData.GetUserData(user, projected);
            if (source is null) continue;
            userData.SaveUserData(user, projected, watchStates.Merge(source, target), UserDataSaveReason.Import);
        }
        library.DeleteItem(native, new DeleteOptions { DeleteFileLocation = false });
    }

    public bool IsPlaying(Guid id) => sessions.Sessions.Any(session => session.NowPlayingItem?.Id == id);

    public bool IsIndexed(Projection entry) => Find(entry.StreamPath) is not null;

    public bool IsPathIndexed(string path) => Find(path) is not null;

    public bool IsPathPlaying(string path) => Find(path) is { } item && IsPlaying(item.Id);

    private BaseItem? Find(string path) => library.GetItemList(new InternalItemsQuery { Path = path, Recursive = true })
        .FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.Ordinal));

    private static bool IsWithin(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static void AddIds(XElement root, MediaIdentity identity)
    {
        if (identity.TmdbId > 0) root.Add(new XElement("uniqueid", new XAttribute("type", "tmdb"), identity.TmdbId));
        if (identity.TvdbId > 0) root.Add(new XElement("uniqueid", new XAttribute("type", "tvdb"), identity.TvdbId));
        if (!string.IsNullOrWhiteSpace(identity.ImdbId)) root.Add(new XElement("uniqueid", new XAttribute("type", "imdb"), identity.ImdbId));
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content) return;
        var temporary = path + ".new";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
