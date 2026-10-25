namespace Jellyfin.Plugin.HdrPlayback.Transcoding;

using global::HdrPlayback.Core.Playback;
using global::HdrPlayback.Core.Transcoding;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;

internal sealed class HdrTranscodeManager(ITranscodeManager inner, IHdrSessionStore sessions, IHdrCommandTransformer transformer) : ITranscodeManager, IDisposable
{
    public Task<TranscodingJob> StartFfMpeg(
        StreamState state,
        string outputPath,
        string commandLineArguments,
        Guid userId,
        TranscodingJobType transcodingJobType,
        CancellationTokenSource cancellationTokenSource,
        string? workingDirectory = null)
    {
        var key = new HdrSessionKey(userId, state.Request.DeviceId ?? "", state.Request.PlaySessionId ?? "");
        if (sessions.Contains(key))
        {
            commandLineArguments = transformer.Transform(commandLineArguments);
        }

        // The original manager retains process ownership, permissions, throttling, progress and cleanup.
        return inner.StartFfMpeg(state, outputPath, commandLineArguments, userId, transcodingJobType, cancellationTokenSource, workingDirectory);
    }

    public void Dispose()
    {
        // The decorated singleton is created from the original registration and is now owned by this wrapper.
        (inner as IDisposable)?.Dispose();
    }

    public TranscodingJob? GetTranscodingJob(string playSessionId)
    {
        return inner.GetTranscodingJob(playSessionId);
    }

    public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type)
    {
        return inner.GetTranscodingJob(path, type);
    }

    public void PingTranscodingJob(string playSessionId, bool? isUserPaused)
    {
        inner.PingTranscodingJob(playSessionId, isUserPaused);
    }

    public async Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles)
    {
        await inner.KillTranscodingJobs(deviceId, playSessionId, deleteFiles).ConfigureAwait(false);
        // Native seeking also kills and restarts a job under the same play-session ID. Retain HDR selection until playback actually stops.
    }

    public void ReportTranscodingProgress(
        TranscodingJob job,
        StreamState state,
        TimeSpan? transcodingPosition,
        float? framerate,
        double? percentComplete,
        long? bytesTranscoded,
        int? bitRate)
    {
        inner.ReportTranscodingProgress(job, state, transcodingPosition, framerate, percentComplete, bytesTranscoded, bitRate);
    }

    public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type)
    {
        return inner.OnTranscodeBeginRequest(path, type);
    }

    public void OnTranscodeEndRequest(TranscodingJob job)
    {
        inner.OnTranscodeEndRequest(job);
    }

    public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken)
    {
        return inner.LockAsync(outputPath, cancellationToken);
    }
}
