namespace TransmissionStreaming.TestSupport.Torrents.Seed;

internal static class CatalogEndpoints
{
    public static void Map(WebApplication app, SeedControl control)
    {
        app.MapGet(
            pattern: "/{fileName}",
            handler: (string fileName) =>
            {
                var root = control.CatalogRoot;
                if (root is null || fileName is not ("manifest.json" or "package.zip"))
                {
                    return Results.NotFound();
                }

                var jsonFile = fileName.EndsWith(".json", StringComparison.Ordinal);
                var contentType = jsonFile ? "application/json" : "application/zip";
                var path = Path.Combine(root, fileName);
                return Results.File(path, contentType);
            }
        );
    }
}
