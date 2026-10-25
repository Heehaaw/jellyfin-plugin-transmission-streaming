namespace TransmissionStreaming.IntegrationTests.Arr;

using global::TransmissionStreaming.Core.Arr;

// Common wire evidence setup keeps each regression focused on the changed fact.
internal static class WireScenarioSetup
{
    public static void PrepareMovie(WireScenario source)
    {
        source.Identity.Kind = MediaKind.Movie;
        var row = source.Queue.Records[0];
        row.DownloadId = source.Identity.DownloadHash;
        row.MovieId = source.Identity.MovieId;
        source.Queue.TotalRecords = source.Queue.Records.Count;
    }

    public static void PrepareEpisode(WireScenario source)
    {
        source.Identity.Kind = MediaKind.Episode;
        source.Identity.SeasonNumber %= 50;
        source.Identity.EpisodeNumber = source.Identity.EpisodeNumber % 90 + 1;
        source.Identity.OriginalFileName = $"{source.Identity.Title}.S{source.Identity.SeasonNumber:00}E{source.Identity.EpisodeNumber:00}.mkv";
        var row = source.Queue.Records[0];
        row.DownloadId = source.Identity.DownloadHash;
        row.SeriesId = source.Identity.SeriesId;
        source.Queue.TotalRecords = source.Queue.Records.Count;
        var episode = source.Episodes[0];
        episode.Id = source.Identity.EpisodeId;
        episode.SeasonNumber = source.Identity.SeasonNumber;
        episode.EpisodeNumber = source.Identity.EpisodeNumber;
    }

    public static void PrepareImport(WireScenario source)
    {
        source.File.MovieId = source.Identity.MovieId;
        source.File.SeriesId = source.Identity.SeriesId;
        source.File.Path = "/media/" + source.File.Path + ".mkv";
        var row = source.History.Records[0];
        row.DownloadId = source.Identity.DownloadHash;
        row.MovieId = source.Identity.MovieId;
        row.SeriesId = source.Identity.SeriesId;
        row.EpisodeId = source.Identity.EpisodeId;
        row.EventType = "downloadFolderImported";
        row.Data["droppedPath"] = "/downloads/" + source.Identity.OriginalFileName;
        row.Data["importedPath"] = source.File.Path;
        row.Data["size"] = source.File.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
        row.Data["fileId"] = source.File.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        source.History.TotalRecords = source.History.Records.Count;
    }
}
