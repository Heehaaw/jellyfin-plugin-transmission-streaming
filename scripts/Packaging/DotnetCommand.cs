namespace TransmissionStreaming.Packaging;

using System.Diagnostics;

internal static class DotnetCommand
{
    public static async Task<string> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, bool quiet, CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo
        (
            workingDirectory: workingDirectory,
            arguments: arguments
        );
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the .NET SDK.");
        // The SDK process owns build workers. Cancelling a script must not leave a build running behind it.
        using var cancellation = cancellationToken.Register(() => CancelProcess(process));
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await standardOutput;
        var error = await standardError;
        if (!quiet || process.ExitCode != 0)
        {
            Console.Write(output);
            Console.Error.Write(error);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"The .NET SDK command failed with exit code {process.ExitCode}.");
        }

        return output.Trim();
    }

    private static ProcessStartInfo CreateStartInfo(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Reused MSBuild workers can retain redirected output handles after the build exits.
        // Keep those workers within this invocation so output collection has a bounded lifetime.
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void CancelProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process can exit between the state check and the cancellation request.
        }
    }
}
