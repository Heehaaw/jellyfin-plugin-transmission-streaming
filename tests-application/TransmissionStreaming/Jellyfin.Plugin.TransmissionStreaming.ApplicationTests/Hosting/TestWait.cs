namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using global::TransmissionStreaming.TestSupport;

internal static class TestWait
{
    public static async Task UntilAsync(Func<Task<bool>> probe, string description, int seconds = 45)
    {
        await Poll.UntilAsync(probe, ready => ready, description, CancellationToken.None, TimeSpan.FromSeconds(seconds));
    }
}
