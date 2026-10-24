namespace TransmissionStreaming.Core.Arr;

/// <summary>Stable media identity retained after the download leaves the arr queue.</summary>
public sealed record MediaIdentity
{
    public MediaKind Kind { get; set; }
    public int MovieId { get; set; }
    public int SeriesId { get; set; }
    public int EpisodeId { get; set; }
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EpisodeTitle { get; set; } = string.Empty;
    public int Year { get; set; }
    public int TmdbId { get; set; }
    public int TvdbId { get; set; }
    public string ImdbId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string DownloadHash { get; set; } = string.Empty;
}
