namespace TransmissionStreaming.Infrastructure.Tests.Arr;

using global::TransmissionStreaming.Core.Arr;

public sealed class TestHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond
    {
        get;
        set;
    } = _ => throw new InvalidOperationException("Unexpected request.");

    public List<Uri> Requests
    {
        get;
    } = [];

    public List<string> ApiKeys
    {
        get;
    } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        ApiKeys.Add(request.Headers.GetValues("X-Api-Key").Single());
        return Task.FromResult(Respond(request));
    }
}

public sealed record WireScenario
{
    public ArrConnection Connection
    {
        get;
        set;
    } = null!;

    public MediaIdentity Identity
    {
        get;
        set;
    } = null!;

    public WirePage Queue
    {
        get;
        set;
    } = null!;

    public WirePage History
    {
        get;
        set;
    } = null!;

    public WireFile File
    {
        get;
        set;
    } = null!;

    public List<WireEpisode> Episodes
    {
        get;
        set;
    } = null!;
}

public sealed record WirePage
{
    public List<WireRecord> Records
    {
        get;
        set;
    } = null!;

    public int TotalRecords
    {
        get;
        set;
    }
}

public sealed record WireRecord
{
    public string DownloadId
    {
        get;
        set;
    } = string.Empty;

    public int MovieId
    {
        get;
        set;
    }

    public int SeriesId
    {
        get;
        set;
    }

    public int EpisodeId
    {
        get;
        set;
    }

    public string EventType
    {
        get;
        set;
    } = string.Empty;

    public WireMedia Movie
    {
        get;
        set;
    } = null!;

    public WireMedia Series
    {
        get;
        set;
    } = null!;

    public Dictionary<string, string> Data
    {
        get;
        set;
    } = null!;
}

public sealed record WireMedia
{
    public string Title
    {
        get;
        set;
    } = string.Empty;

    public int Year
    {
        get;
        set;
    }

    public int TmdbId
    {
        get;
        set;
    }

    public int TvdbId
    {
        get;
        set;
    }

    public string ImdbId
    {
        get;
        set;
    } = string.Empty;
}

public sealed record WireEpisode
{
    public int Id
    {
        get;
        set;
    }

    public int SeasonNumber
    {
        get;
        set;
    }

    public int EpisodeNumber
    {
        get;
        set;
    }

    public int EpisodeFileId
    {
        get;
        set;
    }

    public string Title
    {
        get;
        set;
    } = string.Empty;
}

public sealed record WireFile
{
    public int Id
    {
        get;
        set;
    }

    public int MovieId
    {
        get;
        set;
    }

    public int SeriesId
    {
        get;
        set;
    }

    public string Path
    {
        get;
        set;
    } = string.Empty;

    public long Size
    {
        get;
        set;
    }
}
