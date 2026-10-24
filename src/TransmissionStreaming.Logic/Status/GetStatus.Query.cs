namespace TransmissionStreaming.Logic.Status;

using Mediator;
using Microsoft.Extensions.Options;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Discovery;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Streaming;

/// <summary>Defines the GetStatus query and its application handler.</summary>
public static class GetStatus
{
    /// <summary>Requests a credential-free snapshot of discovery and active readers.</summary>
    public sealed record Query : IQuery<StreamingStatus>;

    /// <summary>Projects diagnostics without returning connection details or capability secrets.</summary>
    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IDiscoveryStatus discovery,
        IProjectionStore store,
        IStreamActivity activity) : IQueryHandler<Query, StreamingStatus>
    {
        public ValueTask<StreamingStatus> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = discovery.Get();
            var entries = store.All().Select(entry => new StreamStatus(entry.Id, entry.Identity.Title,
                entry.Status, entry.Progress, activity.Count(entry.Id))).ToArray();
            return ValueTask.FromResult(new StreamingStatus(options.CurrentValue.Enabled, status.LastSuccessfulPoll, status.LastFailure, entries));
        }
    }
}

/// <summary>Discovery diagnostics safe to expose to an administrator.</summary>
public sealed record StreamingStatus(bool Enabled, DateTimeOffset? LastSuccessfulPoll, string? LastFailure, IReadOnlyList<StreamStatus> Entries);

/// <summary>Progress and reader counts for a single projected title.</summary>
public sealed record StreamStatus(string Id, string Title, string Status, double Progress, int Readers);
