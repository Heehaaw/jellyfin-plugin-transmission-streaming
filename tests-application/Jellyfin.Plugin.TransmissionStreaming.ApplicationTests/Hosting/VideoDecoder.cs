namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using DotNet.Testcontainers.Containers;

/// <summary>Decodes synthetic media inside Jellyfin's image without logging stream capabilities.</summary>
internal sealed class VideoDecoder(IContainer container)
{
    private const string Binary = "/usr/lib/jellyfin-ffmpeg/ffmpeg";

    public Task<string> FileHashAsync(string path, CancellationToken cancellationToken)
    {
        return HashAsync(
            arguments:
            [
                Binary, "-hide_banner", "-loglevel", "error", "-i", path,
                "-map", "0:v:0", "-f", "hash", "-hash", "sha256", "-"
            ],
            cancellationToken: cancellationToken
        );
    }

    public Task<string> StreamHashAsync(string projectionPath, CancellationToken cancellationToken)
    {
        // Exec arguments appear in container diagnostics. Read the capability inside the shell
        // so neither Testcontainers nor an assertion failure can print the stream secret.
        const string decode = """
                              IFS= read -r stream_url < "$1"
                              exec /usr/lib/jellyfin-ffmpeg/ffmpeg -hide_banner -loglevel error -i "$stream_url" -map 0:v:0 -f hash -hash sha256 -
                              """;
        return HashAsync(["/bin/sh", "-c", decode, "decode", projectionPath], cancellationToken);
    }

    private async Task<string> HashAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var result = await container.ExecAsync(arguments, cancellationToken);
        if (result.ExitCode != 0 || !result.Stdout.Trim().StartsWith("SHA256=", StringComparison.Ordinal))
        {
            // FFmpeg errors may include its input URL, so retain only a sanitized outcome.
            throw new InvalidOperationException($"Synthetic media decoding failed (exit {result.ExitCode}).");
        }

        return result.Stdout.Trim();
    }
}
