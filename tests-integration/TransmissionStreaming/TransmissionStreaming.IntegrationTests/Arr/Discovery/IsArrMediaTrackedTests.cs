namespace TransmissionStreaming.IntegrationTests.Arr.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using global::TransmissionStreaming.Logic.Discovery;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class IsArrMediaTrackedTests
{
    [Theory, AutoMoqData]
    internal async Task Should_CheckTheMovieLibraryRatherThanTheQueue(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        source.Library[0].Id = source.Identity.MovieId;
        handler.Respond = _ => Response(Library(source).GetRawText());

        // Act
        var result = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        handler.Requests.Should().ContainSingle(request => request.AbsolutePath == "/api/v3/movie");
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveEpisodesWhileTheirSeriesRemainsTracked(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Episode;
        source.Library[0].Id = source.Identity.SeriesId;
        handler.Respond = _ => Response(Library(source).GetRawText());

        // Act
        var result = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        handler.Requests.Should().ContainSingle(request => request.AbsolutePath == "/api/v3/series");
    }

    [Theory, AutoMoqData]
    internal async Task Should_MatchAReaddedMovieByTmdbIdentity(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        var row = source.Library[0];
        row.Id = source.Identity.MovieId + 1;
        row.TmdbId = source.Identity.TmdbId;
        row.TvdbId = 0;
        row.ImdbId = "";
        handler.Respond = _ => Response(Library(source).GetRawText());

        // Act
        var result = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal async Task Should_MatchAReaddedSeriesByTvdbIdentity(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Episode;
        var row = source.Library[0];
        row.Id = source.Identity.SeriesId + 1;
        row.TmdbId = 0;
        row.TvdbId = source.Identity.TvdbId;
        row.ImdbId = "";
        handler.Respond = _ => Response(Library(source).GetRawText());

        // Act
        var result = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal async Task Should_MatchAReaddedTitleByPositiveImdbIdentity(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        source.Identity.ImdbId = "tt" + source.Identity.TmdbId;
        var row = source.Library[0];
        row.Id = source.Identity.MovieId + 1;
        row.TmdbId = 0;
        row.TvdbId = 0;
        row.ImdbId = source.Identity.ImdbId.ToUpperInvariant();
        handler.Respond = _ => Response(Library(source).GetRawText());

        // Act
        var result = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReportAbsenceOnlyFromAnAuthoritativeCollection(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        source.Library.Clear();
        handler.Respond = _ => Response(Library(source).GetRawText());

        // Act
        var result = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveUnknownOrUnidentifiableMediaWithoutARequest(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        var unknown = source.Identity with
        {
            Kind = (MediaKind)(-1)
        };
        var unidentified = source.Identity with
        {
            Kind = MediaKind.Movie,
            MovieId = 0,
            TmdbId = 0,
            TvdbId = 0,
            ImdbId = "tt0000"
        };

        // Act
        var unknownRemains = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, unknown), TestContext.Current.CancellationToken);
        var unidentifiedRemains = await sut.Handle(new IsArrMediaTracked.Query(source.Connection, unidentified), TestContext.Current.CancellationToken);

        // Assert
        unknownRemains.Should().BeTrue();
        unidentifiedRemains.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectMalformedLibraryShapes(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        string[] malformed = ["{}", "null", "[null]", "[{}]", "not-json"];

        foreach (var payload in malformed)
        {
            handler.Respond = _ => Response(payload);

            // Act
            var action = async () => await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<JsonException>();
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_PropagateAnUnavailableLibrary(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act
        var action = async () => await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotTreatAnEndpoint404AsADeletedTitle(TrackingScenario source, TestHttpHandler handler, IsArrMediaTracked.Handler sut)
    {
        // Arrange
        source.Identity.Kind = MediaKind.Movie;
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        // Act
        var action = async () => await sut.Handle(new IsArrMediaTracked.Query(source.Connection, source.Identity), TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<ArrNotFoundException>();
    }

    private static HttpResponseMessage Response(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static JsonElement Library(TrackingScenario source)
    {
        return JsonSerializer.SerializeToElement(source.Library, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    internal sealed record TrackingScenario(ArrConnection Connection, MediaIdentity Identity, List<TrackedMedia> Library);

    internal sealed record TrackedMedia
    {
        public int Id { get; set; }

        public int TmdbId { get; set; }

        public int TvdbId { get; set; }

        public string ImdbId { get; set; } = "";
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
            fixture.Register(() => new HttpClient(fixture.Create<TestHttpHandler>()));
            fixture.Freeze<TestHttpHandler>();
            return fixture;
        }
    );
}
