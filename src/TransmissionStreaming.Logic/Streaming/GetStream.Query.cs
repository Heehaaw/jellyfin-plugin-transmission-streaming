namespace TransmissionStreaming.Logic.Streaming;

using Mediator;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;

/// <summary>Defines the GetStream query and its application handler.</summary>
public static class GetStream
{
    /// <summary>Authorizes a stable stream capability without exposing its secret in diagnostics.</summary>
    public sealed record Query(string Id, string? Key) : IQuery<Projection?>
    {
        public override string ToString()
        {
            return "GetStream (capability redacted)";
        }
    }

    /// <summary>Checks enablement and capability equality before returning any playback metadata.</summary>
    internal sealed class Handler(IOptionsMonitor<StreamingOptions> options, IProjectionStore store) : IQueryHandler<Query, Projection?>
    {
        public ValueTask<Projection?> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!options.CurrentValue.Enabled || query.Key is null)
            {
                return ValueTask.FromResult<Projection?>(null);
            }

            var entry = store.Get(query.Id);
            if (entry is null || string.IsNullOrEmpty(entry.Secret) || entry.Secret.Length != query.Key.Length)
            {
                return ValueTask.FromResult<Projection?>(null);
            }

            var valid = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(entry.Secret), Encoding.UTF8.GetBytes(query.Key));
            return ValueTask.FromResult(valid ? entry : null);
        }
    }
}
