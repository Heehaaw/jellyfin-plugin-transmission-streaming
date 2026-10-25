namespace TransmissionStreaming.Logic.Projection;

using Core.Arr;
using Mediator;
using System.Security.Cryptography;
using System.Text;

/// <summary>Calculates permanent projection IDs and generated library paths.</summary>
public static class BuildProjectionPaths
{
    /// <summary>Requests names without accessing or creating files.</summary>
    public sealed record Query(string Root, MediaIdentity Identity) : IQuery<Result>;

    /// <summary>Stable identity and display path retained independently of download progress.</summary>
    public sealed record Result(string Id, string StreamPath);

    internal sealed class Handler : IQueryHandler<Query, Result>
    {
        public ValueTask<Result> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new Result(Id(query.Identity), StreamPath(query.Root, query.Identity)));
        }

        private static string IdentityKey(MediaIdentity identity)
        {
            return identity.Kind == MediaKind.Movie ? $"movie:{identity.MovieId}" : $"episode:{identity.SeriesId}:{identity.SeasonNumber}:{identity.EpisodeNumber}";
        }

        private static string Id(MediaIdentity identity)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(IdentityKey(identity))))[..32];
        }

        private static string StreamPath(string root, MediaIdentity identity)
        {
            var title = SafeName(identity.Title);
            var folder = identity.Year > 0 ? $"{title} ({identity.Year})" : title;
            return identity.Kind == MediaKind.Movie
                ? Path.Combine(root, "movies", folder, folder + ".strm")
                : Path.Combine(root, "shows", folder, $"Season {identity.SeasonNumber:00}", $"{title} - S{identity.SeasonNumber:00}E{identity.EpisodeNumber:00}.strm");
        }

        private static string SafeName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
            var name = new string(value.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().Trim('.');
            return string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
        }
    }
}
