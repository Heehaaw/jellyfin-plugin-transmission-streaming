namespace TransmissionStreaming.TestSupport.Torrents.Seed;

internal static class CatalogEndpoints
{
    public static void Map(WebApplication app, SeedControl control)
    {
        app.MapGet("/{fileName}", (string fileName) =>
        {
            var root = control.CatalogRoot;
            if (root is null || fileName is not ("manifest.json" or "package.zip"))
            {
                return Results.NotFound();
            }
            var contentType = fileName.EndsWith(".json", StringComparison.Ordinal) ? "application/json" : "application/zip";
            return Results.File(Path.Combine(root, fileName), contentType);
        });
    }
}
