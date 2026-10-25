namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Ui;

using Hosting;
using Microsoft.Playwright;

/// <summary>Signs into the disposable server through the same web flow as an administrator.</summary>
internal sealed class JellyfinDashboard(IPage page)
{
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        var login = new Uri(JellyfinContainer.InternalAddress, "/web/#/login");
        await page.GotoAsync(login.AbsoluteUri).WaitAsync(cancellationToken);
        await page.Locator("input[name=Username], #txtManualName").FillAsync(JellyfinTestAccount.Username);
        await page.Locator("input[type=password]").FillAsync(JellyfinTestAccount.Password);
        await page.GetByRole(
            role: AriaRole.Button,
            options: new()
            {
                Name = "Sign In",
                Exact = true
            }
        ).ClickAsync();
        await page.WaitForURLAsync("**/home*");
        var dashboard = new Uri(JellyfinContainer.InternalAddress, "/web/#/dashboard");
        await page.GotoAsync(dashboard.AbsoluteUri).WaitAsync(cancellationToken);
    }
}
