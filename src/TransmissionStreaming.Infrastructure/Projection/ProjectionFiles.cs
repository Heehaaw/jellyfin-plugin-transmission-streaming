namespace TransmissionStreaming.Infrastructure.Projection;

using Core.Configuration;
using Core.Projection;
using global::TransmissionStreaming.Core.Arr;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml.Linq;
using ProjectionEntry = Core.Projection.Projection;

/// <summary>Writes stable stream and metadata files without a media-server dependency.</summary>
internal sealed class ProjectionFiles(IOptionsMonitor<StreamingOptions> options) : IProjectionFiles
{
    private const string OwnershipMarker = ".transmission-streaming";
    private const string OwnershipValue = "TransmissionStreaming";

    public string Root
    {
        get
        {
            var config = options.CurrentValue;
            return string.IsNullOrWhiteSpace(config.ProjectionRoot)
                ? Path.Combine(config.StateDirectory, "library")
                : Path.GetFullPath(config.ProjectionRoot);
        }
    }

    public void WriteFiles(ProjectionEntry entry)
    {
        var config = options.CurrentValue;
        var baseUri = new Uri(config.JellyfinPublicUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (baseUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new InvalidOperationException("Jellyfin URL must be HTTP(S), without embedded credentials.");
        }

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
        else
        {
            AddIds(nfo, identity);
        }

        WriteIfChanged(Path.ChangeExtension(entry.StreamPath, ".nfo"), nfo.ToString());
    }

    /// <summary>Claims an empty projection directory, refusing to adopt existing user media.</summary>
    public void EnsureReady()
    {
        var full = Path.GetFullPath(Root);
        if (full == Path.GetPathRoot(full))
        {
            throw new InvalidOperationException("A dedicated projection directory is required.");
        }

        var marker = Path.Combine(full, OwnershipMarker);
        if (File.Exists(marker) && File.ReadAllText(marker) != OwnershipValue)
        {
            throw new InvalidOperationException("The projection directory ownership marker does not match this plugin.");
        }

        if (Directory.Exists(full) && !File.Exists(marker) && Directory.EnumerateFileSystemEntries(full).Any())
        {
            throw new InvalidOperationException("The projection directory must be empty on first use.");
        }

        Directory.CreateDirectory(full);
        if (!File.Exists(marker))
        {
            File.WriteAllText(marker, OwnershipValue);
        }

        Directory.CreateDirectory(Path.Combine(full, "movies"));
        Directory.CreateDirectory(Path.Combine(full, "shows"));
    }

    private static void AddIds(XElement root, MediaIdentity identity)
    {
        if (identity.TmdbId > 0)
        {
            root.Add(new XElement("uniqueid", new XAttribute("type", "tmdb"), identity.TmdbId));
        }

        if (identity.TvdbId > 0)
        {
            root.Add(new XElement("uniqueid", new XAttribute("type", "tvdb"), identity.TvdbId));
        }

        if (!string.IsNullOrWhiteSpace(identity.ImdbId))
        {
            root.Add(new XElement("uniqueid", new XAttribute("type", "imdb"), identity.ImdbId));
        }
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content)
        {
            return;
        }

        var temporary = path + ".new";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
