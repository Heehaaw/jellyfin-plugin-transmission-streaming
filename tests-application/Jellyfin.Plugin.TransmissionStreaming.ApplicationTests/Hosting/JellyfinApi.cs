namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Drives only the disposable server; no host configuration or credentials are reused.</summary>
internal sealed class JellyfinApi(Uri address) : IDisposable
{
    private readonly HttpClient client = new() { BaseAddress = address, Timeout = TimeSpan.FromSeconds(20) };
    private string? token;

    public async Task<JsonNode?> RequestAsync(string path, object? body = null, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? (body is null ? HttpMethod.Get : HttpMethod.Post), path);
        Authorize(request);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: new JsonSerializerOptions { PropertyNamingPolicy = null });
        }

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
    }

    public async Task<byte[]> ReadVideoAsync(string itemId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Videos/{itemId}/stream?static=true");
        Authorize(request);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<string> AuthenticateAsync()
    {
        const string username = "application-test-admin";
        const string password = "Disposable-application-test-only-123!";
        var info = await WaitForAsync(() => RequestAsync("/System/Info/Public"), "Jellyfin public API availability");
        if (!Value<bool>(info, "StartupWizardCompleted"))
        {
            await WaitForAsync(() => RequestAsync("/Startup/User"), "Jellyfin startup API availability");
            await RequestAsync("/Startup/User", new { Name = username, Password = password });
            await RequestAsync("/Startup/Configuration", new
            {
                ServerName = "Disposable application test",
                UICulture = "en-US",
                MetadataCountryCode = "US",
                PreferredMetadataLanguage = "en"
            });
            await RequestAsync("/Startup/RemoteAccess", new { EnableRemoteAccess = true, EnableAutomaticPortMapping = false });
            await RequestAsync("/Startup/Complete", new { });
        }

        var authentication = await WaitForAsync(() => RequestAsync("/Users/AuthenticateByName", new { Username = username, Pw = password }),
            "Jellyfin authentication API availability");
        token = Value<string>(authentication, "AccessToken");
        return Value<string>(Field(authentication, "User"), "Id");
    }

    private void Authorize(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Authorization", token is null
            ? "MediaBrowser Client=\"Application tests\", Device=\"Disposable container\", DeviceId=\"transmission-tests\", Version=\"1\""
            : $"MediaBrowser Token=\"{token}\"");
    }

    private static async Task<JsonNode?> WaitForAsync(Func<Task<JsonNode?>> request, string description)
    {
        JsonNode? result = null;
        await TestWait.UntilAsync(async () =>
        {
            try
            {
                result = await request();
                return true;
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null or System.Net.HttpStatusCode.ServiceUnavailable)
            {
                return false;
            }
        }, description);
        return result;
    }

    public static JsonNode? Field(JsonNode? value, string name)
    {
        return value?.AsObject().FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    public static T Value<T>(JsonNode? value, string name)
    {
        return Field(value, name)!.GetValue<T>();
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
