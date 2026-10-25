namespace Jellyfin.Plugin.HdrPlayback.Player;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

internal sealed class HdrUiStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<HdrUiMiddleware>();
            next(app);
        };
    }
}
