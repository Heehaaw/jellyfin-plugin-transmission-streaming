namespace Jellyfin.Plugin.HdrPlayback.Player;

using Microsoft.AspNetCore.Http;
using System.Text;

internal sealed class HdrUiMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        foreach (var script in new[] { "player.js", "source-selector.js" })
        {
            if (HttpMethods.IsGet(context.Request.Method) && path.EndsWith("/HdrPlayback/" + script, StringComparison.OrdinalIgnoreCase))
            {
                await ServePlayerScript(context, script).ConfigureAwait(false);
                return;
            }
        }

        var index = path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
        var chunk = path.EndsWith(".chunk.js", StringComparison.OrdinalIgnoreCase);
        if (!HttpMethods.IsGet(context.Request.Method) || (!index && !chunk))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Transform uncompressed full responses; file validators no longer describe an extended page or menu.
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("If-None-Match");
        context.Request.Headers.Remove("If-Modified-Since");
        var original = context.Response.Body;
        await using var buffered = new MemoryStream();
        context.Response.Body = buffered;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = original;
        }

        var bytes = buffered.ToArray();
        if (context.Response.StatusCode == StatusCodes.Status200OK && bytes.Length is > 0 and < 2 * 1024 * 1024 && !context.Response.Headers.ContainsKey("Content-Encoding"))
        {
            var text = Encoding.UTF8.GetString(bytes);
            var webIndex = path.LastIndexOf("/web", StringComparison.OrdinalIgnoreCase);
            var basePath = context.Request.PathBase + (webIndex < 0 ? "" : path[..webIndex]);
            var edited = index ? PlayerScriptInjector.InjectIndex(text, basePath) : PlayerScriptInjector.InjectMenu(text);
            if (edited != text)
            {
                bytes = Encoding.UTF8.GetBytes(edited);
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.Remove("Last-Modified");
                context.Response.Headers.CacheControl = "no-store";
                context.Response.ContentLength = bytes.Length;
            }
        }

        await original.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task ServePlayerScript(HttpContext context, string script)
    {
        await using var resource = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.HdrPlayback.Player." + script)
                                   ?? throw new InvalidOperationException("The HDR playback script is missing from the plugin bundle.");
        context.Response.ContentType = "application/javascript; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await resource.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }
}
