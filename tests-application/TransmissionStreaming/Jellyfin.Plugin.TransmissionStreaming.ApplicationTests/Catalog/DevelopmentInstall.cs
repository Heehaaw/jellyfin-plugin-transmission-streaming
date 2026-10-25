namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using System.Diagnostics;

/// <summary>Runs the documented local installation command, including its default Debug build.</summary>
internal static class DevelopmentInstall
{
    public static async Task<string> RunAsync(string pluginsDirectory, CancellationToken cancellationToken)
    {
        var root = FindRepositoryRoot();
        var script = Path.Combine(root, "scripts", "Install.cs");
        await DotnetAsync(root, ["run", "--file", script, "--plugins-dir", pluginsDirectory], cancellationToken);
        var project = Path.Combine(root, "src", "TransmissionStreaming", "Jellyfin.Plugin.TransmissionStreaming", "Jellyfin.Plugin.TransmissionStreaming.csproj");
        return (await DotnetAsync(root, ["msbuild", project, "-property:Configuration=Debug", "-getProperty:TargetDir", "-nologo"], cancellationToken)).Trim();
    }

    internal static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "JellyfinPlugins.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The local development installation test must run from the repository checkout.");
    }

    private static async Task<string> DotnetAsync(string root, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Worker reuse can retain redirected output handles after the build process exits.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the .NET installation command.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            throw;
        }

        var text = await output;
        var errorText = await errors;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Development installation command failed ({process.ExitCode}).\n{text}\n{errorText}");
        }

        return text;
    }
}
