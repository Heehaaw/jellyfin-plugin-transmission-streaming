namespace TransmissionStreaming.IntegrationTests.Arr.Imports;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr;
using global::TransmissionStreaming.Logic.Imports;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using static WireScenarioSetup;

public sealed class FindArrImportedFileTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ResolveOnlyConfirmedImport(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal) ? Response(source.History) : Response(source.File);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, source.File.Size), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(source.File.Path);
        handler.Requests[0].Query.Should().Contain("downloadId=" + source.Identity.DownloadHash);
        handler.Requests[1].AbsolutePath.Should().EndWith("/moviefile/" + source.File.Id);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectExistingFileWithoutMatchingImport(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        source.History.Records[0].DownloadId += "different";
        handler.Respond = _ => Response(source.History);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, source.File.Size), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectChangedFileSize(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        var expectedSize = source.File.Size;
        source.File.Size++;
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal) ? Response(source.History) : Response(source.File);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, expectedSize), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAnotherFileWithinSameTorrent(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        source.History.Records[0].Data["droppedPath"] += ".other";
        handler.Respond = _ => Response(source.History);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, source.File.Size), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectFileRemovedAfterImport(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? Response(source.History)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, source.File.Size), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResolveConfirmedEpisodeImport(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareEpisode(source);
        PrepareImport(source);
        source.Episodes[0].EpisodeFileId = source.File.Id;
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal) ? Response(source.History) :
            request.RequestUri.AbsolutePath.EndsWith("/episode", StringComparison.Ordinal) ? Response(source.Episodes) : Response(source.File);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, source.File.Size), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(source.File.Path);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectEpisodeFileLinkedToAnotherEpisode(WireScenario source, TestHttpHandler handler, FindArrImportedFile.Handler sut)
    {
        // Arrange
        PrepareEpisode(source);
        PrepareImport(source);
        source.Episodes[0].EpisodeFileId = source.File.Id;
        source.Episodes[0].Id++;
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal) ? Response(source.History) :
            request.RequestUri.AbsolutePath.EndsWith("/episode", StringComparison.Ordinal) ? Response(source.Episodes) : Response(source.File);

        // Act
        var result = await sut.Handle(new FindArrImportedFile.Query(source.Connection, source.Identity, source.File.Size), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
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
