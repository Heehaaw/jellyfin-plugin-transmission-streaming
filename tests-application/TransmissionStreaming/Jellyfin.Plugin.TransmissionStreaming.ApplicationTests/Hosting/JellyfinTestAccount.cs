namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

/// <summary>Credentials created only inside disposable application-test servers.</summary>
internal static class JellyfinTestAccount
{
    public const string Username = "application-test-admin";
    public const string Password = "Disposable-application-test-only-123!";
}
