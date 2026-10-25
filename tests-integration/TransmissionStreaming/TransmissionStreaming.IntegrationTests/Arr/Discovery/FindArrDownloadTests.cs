namespace TransmissionStreaming.IntegrationTests.Arr.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr;
using global::TransmissionStreaming.Logic.Discovery;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using static WireScenarioSetup;

public sealed class FindArrDownloadTests
{
    [Theory, AutoMoqData]
    internal async Task Should_DiscoverMovieFromMatchingDownload(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        source.Identity.OriginalFileName = Path.ChangeExtension(source.Identity.OriginalFileName, ".mov");
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(
                Connection: source.Connection,
                Kind: MediaKind.Movie,
                Hash: source.Identity.DownloadHash.ToLowerInvariant(),
                Filename: source.Identity.OriginalFileName
            ),
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Should().NotBeNull();
        result!.MovieId.Should().Be(source.Queue.Records[0].MovieId);
        result.Title.Should().Be(source.Queue.Records[0].Movie.Title);
        result.DownloadHash.Should().Be(source.Identity.DownloadHash);
        handler.Requests.Should().ContainSingle();
        handler.ApiKeys.Should().OnlyContain(key => key == source.Connection.ApiKey);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReadEveryQueuePage(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        source.Queue.TotalRecords = source.Queue.Records.Count * 2;
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(Connection: source.Connection, Kind: MediaKind.Movie, Hash: source.Identity.DownloadHash, Filename: source.Identity.OriginalFileName),
            TestContext.Current.CancellationToken
        );

        // Assert
        result!.MovieId.Should().Be(source.Identity.MovieId);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Query.Should().Contain("page=2");
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAmbiguousMovieMapping(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        var other = source.History.Records[0];
        other.DownloadId = source.Identity.DownloadHash;
        other.MovieId = source.Identity.MovieId + 1;
        source.Queue.Records.Add(other);
        source.Queue.TotalRecords = source.Queue.Records.Count;
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(Connection: source.Connection, Kind: MediaKind.Movie, Hash: source.Identity.DownloadHash, Filename: source.Identity.OriginalFileName),
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_MapEpisodeFromSeasonPackFilename(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        PrepareEpisode(source);
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/queue", StringComparison.Ordinal) ? Response(source.Queue) : Response(source.Episodes);

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(Connection: source.Connection, Kind: MediaKind.Episode, Hash: source.Identity.DownloadHash, Filename: source.Identity.OriginalFileName),
            TestContext.Current.CancellationToken
        );

        // Assert
        result!.SeriesId.Should().Be(source.Identity.SeriesId);
        result.EpisodeId.Should().Be(source.Identity.EpisodeId);
        result.SeasonNumber.Should().Be(source.Identity.SeasonNumber);
        result.EpisodeNumber.Should().Be(source.Identity.EpisodeNumber);
        result.Title.Should().Be(source.Queue.Records[0].Series.Title);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectMultiEpisodeFiles(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        PrepareEpisode(source);
        var filename = $"{source.Identity.Title}.S01E02E03.mkv";
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(Connection: source.Connection, Kind: MediaKind.Episode, Hash: source.Identity.DownloadHash, Filename: filename),
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal async Task Should_IgnoreSampleVideos(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        var filename = source.Identity.Title + ".sample.mkv";

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(Connection: source.Connection, Kind: MediaKind.Movie, Hash: source.Identity.DownloadHash, Filename: filename),
            TestContext.Current.CancellationToken
        );

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowQualityFollowingEpisodeNumber(WireScenario source, TestHttpHandler handler, FindArrDownload.Handler sut)
    {
        // Arrange
        PrepareEpisode(source);
        source.Identity.OriginalFileName = Path.GetFileNameWithoutExtension(source.Identity.OriginalFileName) + " - 1080p.mkv";
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/queue", StringComparison.Ordinal) ? Response(source.Queue) : Response(source.Episodes);

        // Act
        var result = await sut.Handle(
            new FindArrDownload.Query(Connection: source.Connection, Kind: MediaKind.Episode, Hash: source.Identity.DownloadHash, Filename: source.Identity.OriginalFileName),
            TestContext.Current.CancellationToken
        );

        // Assert
        result!.EpisodeId.Should().Be(source.Identity.EpisodeId);
    }

    private static HttpResponseMessage Response<T>(T body)
    {
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
        };
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            fixture.Register<IArrTransport>(() => fixture.Create<ArrTransport>());
            fixture.Register<IArrClient>(() => fixture.Create<ArrClient>());

            fixture.Customize<ArrConnection>(composer => composer.With(connection => connection.Url, "http://arr.invalid/"));
            fixture.Customize<MediaIdentity>(composer => composer.With(identity => identity.OriginalFileName, () => fixture.Create<string>() + ".mkv"));
            fixture.Register(() => new HttpClient(fixture.Create<TestHttpHandler>()));
            fixture.Freeze<TestHttpHandler>();
            return fixture;
        }
    );
}
