namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Text.Json;

internal static class ControlEndpoints
{
    public static void Map(WebApplication app, SeedControl control)
    {
        app.MapPost
        (
            pattern: "/control/{operation}",
            handler: async (string operation, HttpContext context) =>
            {
                using var body = await JsonDocument.ParseAsync
                (
                    utf8Json: context.Request.Body,
                    cancellationToken: context.RequestAborted
                );
                var value = body.RootElement;
                if (operation == "scenario")
                {
                    var directory = value.GetProperty("directory").GetString()!;
                    var configurationPath = Path.Combine
                    (
                        path1: directory,
                        path2: "seed.json"
                    );
                    var json = await File.ReadAllTextAsync
                    (
                        path: configurationPath,
                        cancellationToken: context.RequestAborted
                    );
                    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                    var configuration = JsonSerializer.Deserialize<SeedConfiguration>
                    (
                        json: json,
                        options: options
                    )!;
                    var mediaPath = Path.Combine
                    (
                        path1: directory,
                        path2: "reference.mkv"
                    );
                    var media = await File.ReadAllBytesAsync
                    (
                        path: mediaPath,
                        cancellationToken: context.RequestAborted
                    );
                    control.Reset
                    (
                        next: configuration,
                        bytes: media
                    );
                }
                else if (operation == "catalog")
                {
                    control.CatalogRoot = value.GetProperty("directory").GetString();
                }
                else if (operation == "end")
                {
                    control.Dispose();
                    control.CatalogRoot = null;
                    if (control.Current.PeerFailure is { } failure)
                    {
                        throw new InvalidOperationException(failure);
                    }
                }
                else
                {
                    var scenario = control.Current;
                    lock (scenario.Gate)
                    {
                        scenario.State = operation switch
                        {
                            "first" => scenario.State with
                            {
                                Allowed = 1
                            },
                            "all" => scenario.State with
                            {
                                Allowed = scenario.PieceCount
                            },
                            "import" => scenario.State with
                            {
                                ImportedPath = value.GetProperty("path").GetString(),
                                HistoryHash = value.TryGetProperty
                                (
                                    propertyName: "hash",
                                    value: out var hash
                                )
                                ? hash.GetString()! : scenario.Configuration.Hash
                            },
                            "failure" => scenario.State with
                            {
                                Failure = value.GetProperty("enabled").GetBoolean()
                            },
                            "removed" => scenario.State with
                            {
                                Removed = value.GetProperty("enabled").GetBoolean()
                            },
                            "episode" => scenario.State with
                            {
                                Episode = value.GetProperty("enabled").GetBoolean()
                            },
                            _ => throw new InvalidOperationException("Unknown fixture control operation.")
                        };
                    }
                }

                return Results.Json(new
                {
                });
            }
        );
    }
}
