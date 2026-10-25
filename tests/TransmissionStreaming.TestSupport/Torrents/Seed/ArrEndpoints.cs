namespace TransmissionStreaming.TestSupport.Torrents.Seed;

internal static class ArrEndpoints
{
    public static void Map(WebApplication app, SeedControl owner)
    {
        app.MapGet
        (
            pattern: "/api/v3/{**path}",
            handler: (string path, HttpContext context) =>
            {
                var control = owner.Current;
                var configuration = control.Configuration;
                var media = control.Media;
                if (context.Request.Headers["X-Api-Key"] != control.Configuration.ArrKey)
                {
                    return Results.StatusCode(401);
                }

                SeedState state;
                lock (control.Gate)
                {
                    state = control.State;
                }

                if (state.Failure)
                {
                    return Results.StatusCode(503);
                }

                var movie = new
                {
                    id = 1,
                    title = configuration.Title,
                    year = DateTime.UtcNow.Year,
                    tmdbId = 0,
                    imdbId = ""
                };
                var series = new
                {
                    id = 1,
                    title = configuration.Title,
                    year = DateTime.UtcNow.Year,
                    tvdbId = 0,
                    imdbId = ""
                };
                var episode = new
                {
                    id = 2,
                    seriesId = 1,
                    seasonNumber = 1,
                    episodeNumber = 2,
                    title = "Synthetic episode",
                    episodeFileId = state.ImportedPath is null ? 0 : 1
                };

                object[] rows;
                switch (path)
                {
                    case "rootfolder":
                        return Results.Json(new[]
                        {
                            new
                            {
                                path = "/arr/library"
                            }
                        });
                    case "queue":
                        rows = state.Removed || state.ImportedPath is not null
                            ? []
                            : state.Episode
                                ?
                                [
                                    new
                                    {
                                        id = 1,
                                        seriesId = 1,
                                        series,
                                        episodeIds = new[]
                                        {
                                            2
                                        },
                                        downloadId = configuration.Hash
                                    }
                                ]
                                :
                                [
                                    new
                                    {
                                        id = 1,
                                        movieId = 1,
                                        movie,
                                        downloadId = configuration.Hash
                                    }
                                ];
                        return Results.Json(new
                        {
                            records = rows,
                            totalRecords = rows.Length
                        });
                    case "movie":
                        return Results.Json(!state.Removed && !state.Episode
                            ? new[]
                            {
                                movie
                            }
                            : []);
                    case "series":
                        return Results.Json(!state.Removed && state.Episode
                            ? new[]
                            {
                                series
                            }
                            : []);
                    case "movie/1" when !state.Removed && !state.Episode:
                        return Results.Json(movie);
                    case "series/1" when !state.Removed && state.Episode:
                        return Results.Json(series);
                    case "episode" when state.Episode:
                        return Results.Json(new[]
                        {
                            episode
                        });
                    case "history":
                        rows = state.ImportedPath is null
                            ? []
                            :
                            [
                                new
                                {
                                    id = 1,
                                    movieId = state.Episode ? 0 : 1,
                                    seriesId = state.Episode ? 1 : 0,
                                    episodeId = state.Episode ? 2 : 0,
                                    downloadId = state.HistoryHash,
                                    eventType = "downloadFolderImported",
                                    date = DateTimeOffset.UtcNow,
                                    data = new
                                    {
                                        droppedPath = configuration.CompletedRoot + "/" + configuration.FileName,
                                        importedPath = state.ImportedPath,
                                        size = media.Length.ToString(),
                                        fileId = "1"
                                    }
                                }
                            ];
                        return Results.Json(new
                        {
                            records = rows,
                            totalRecords = rows.Length
                        });
                    case "moviefile/1" when state.ImportedPath is not null && !state.Episode:
                        return Results.Json(new
                        {
                            id = 1,
                            movieId = 1,
                            path = state.ImportedPath,
                            size = media.Length
                        });
                    case "episodefile/1" when state.ImportedPath is not null && state.Episode:
                        return Results.Json(new
                        {
                            id = 1,
                            seriesId = 1,
                            path = state.ImportedPath,
                            size = media.Length
                        });
                    default:
                        return Results.NotFound();
                }
            }
        );
    }
}
