namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Text.Json;

internal static class ControlEndpoints
{
    public static void Map(WebApplication app, SeedControl control)
    {
        var configuration = control.Configuration;
        app.MapPost("/control/{operation}", async (string operation, HttpContext context) =>
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var value = body.RootElement;
            lock (control.Gate)
            {
                control.State = operation switch
                {
                    "first" => control.State with { Allowed = 1 },
                    "all" => control.State with { Allowed = control.PieceCount },
                    "import" => control.State with
                    {
                        ImportedPath = value.GetProperty("path").GetString(),
                        HistoryHash = value.TryGetProperty("hash", out var hash) ? hash.GetString()! : configuration.Hash
                    },
                    "failure" => control.State with { Failure = value.GetProperty("enabled").GetBoolean() },
                    "episode" => control.State with { Episode = value.GetProperty("enabled").GetBoolean() },
                    _ => throw new InvalidOperationException("Unknown synthetic fixture control operation.")
                };
            }
            return Results.Json(new { });
        });
    }
}
