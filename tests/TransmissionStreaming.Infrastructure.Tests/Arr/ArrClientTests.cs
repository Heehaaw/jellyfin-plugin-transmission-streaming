namespace TransmissionStreaming.Infrastructure.Tests.Arr;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Infrastructure.Arr;
using global::TransmissionStreaming.Infrastructure.Arr.Discovery;
using global::TransmissionStreaming.Infrastructure.Arr.Imports;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class ArrClientTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReadDistinctConfiguredLibraryRoots(
        ArrConnection connection,
        RootFolder root,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        var missing = root with
        {
            Path = " "
        };
        handler.Respond = _ => Response(
            new[]
            {
                root, missing, root
            }
        );

        // Act
        var result = await sut.GetRootFoldersAsync(connection, CancellationToken.None);

        // Assert
        result.Should().Equal(root.Path);
        handler.Requests.Should().ContainSingle(request => request.AbsolutePath == "/api/v3/rootfolder");
    }

    internal sealed record RootFolder(string Path);

    [Theory, AutoMoqData]
    internal async Task Should_DiscoverMovieFromMatchingDownload(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Movie,
            hash: source.Identity.DownloadHash.ToLowerInvariant(),
            filename: source.Identity.OriginalFileName,
            cancellationToken: CancellationToken.None
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
    internal async Task Should_ReadEveryQueuePage(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        source.Queue.TotalRecords = source.Queue.Records.Count * 2;
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Movie,
            hash: source.Identity.DownloadHash,
            filename: source.Identity.OriginalFileName,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result!.MovieId.Should().Be(source.Identity.MovieId);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Query.Should().Contain("page=2");
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAmbiguousMovieMapping(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
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
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Movie,
            hash: source.Identity.DownloadHash,
            filename: source.Identity.OriginalFileName,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_MapEpisodeFromSeasonPackFilename(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareEpisode(source);
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/queue", StringComparison.Ordinal)
            ? Response(source.Queue)
            : Response(source.Episodes);

        // Act
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Episode,
            hash: source.Identity.DownloadHash,
            filename: source.Identity.OriginalFileName,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result!.SeriesId.Should().Be(source.Identity.SeriesId);
        result.EpisodeId.Should().Be(source.Identity.EpisodeId);
        result.SeasonNumber.Should().Be(source.Identity.SeasonNumber);
        result.EpisodeNumber.Should().Be(source.Identity.EpisodeNumber);
        result.Title.Should().Be(source.Queue.Records[0].Series.Title);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectMultiEpisodeFiles(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareEpisode(source);
        var filename = $"{source.Identity.Title}.S01E02E03.mkv";
        handler.Respond = _ => Response(source.Queue);

        // Act
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Episode,
            hash: source.Identity.DownloadHash,
            filename: filename,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResolveOnlyConfirmedImport(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? Response(source.History)
            : Response(source.File);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, source.File.Size, CancellationToken.None);

        // Assert
        result.Should().Be(source.File.Path);
        handler.Requests[0].Query.Should().Contain("downloadId=" + source.Identity.DownloadHash);
        handler.Requests[1].AbsolutePath.Should().EndWith("/moviefile/" + source.File.Id);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectExistingFileWithoutMatchingImport(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        source.History.Records[0].DownloadId += "different";
        handler.Respond = _ => Response(source.History);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, source.File.Size, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectChangedFileSize(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        var expectedSize = source.File.Size;
        source.File.Size++;
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? Response(source.History)
            : Response(source.File);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, expectedSize, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAnotherFileWithinSameTorrent(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        source.History.Records[0].Data["droppedPath"] += ".other";
        handler.Respond = _ => Response(source.History);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, source.File.Size, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal async Task Should_IgnoreSampleVideos(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        var filename = source.Identity.Title + ".sample.mkv";

        // Act
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Movie,
            hash: source.Identity.DownloadHash,
            filename: filename,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectFileRemovedAfterImport(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareMovie(source);
        PrepareImport(source);
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? Response(source.History)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, source.File.Size, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResolveConfirmedEpisodeImport(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareEpisode(source);
        PrepareImport(source);
        source.Episodes[0].EpisodeFileId = source.File.Id;
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? Response(source.History)
            : request.RequestUri.AbsolutePath.EndsWith("/episode", StringComparison.Ordinal)
                ? Response(source.Episodes)
                : Response(source.File);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, source.File.Size, CancellationToken.None);

        // Assert
        result.Should().Be(source.File.Path);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectEpisodeFileLinkedToAnotherEpisode(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareEpisode(source);
        PrepareImport(source);
        source.Episodes[0].EpisodeFileId = source.File.Id;
        source.Episodes[0].Id++;
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/history", StringComparison.Ordinal)
            ? Response(source.History)
            : request.RequestUri.AbsolutePath.EndsWith("/episode", StringComparison.Ordinal)
                ? Response(source.Episodes)
                : Response(source.File);

        // Act
        var result = await sut.FindImportedFileAsync(source.Connection, source.Identity, source.File.Size, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowQualityFollowingEpisodeNumber(
        WireScenario source,
        [Frozen] TestHttpHandler handler,
        ArrClient sut
    )
    {
        // Arrange
        PrepareEpisode(source);
        source.Identity.OriginalFileName = Path.GetFileNameWithoutExtension(source.Identity.OriginalFileName) + " - 1080p.mkv";
        handler.Respond = request => request.RequestUri!.AbsolutePath.EndsWith("/queue", StringComparison.Ordinal)
            ? Response(source.Queue)
            : Response(source.Episodes);

        // Act
        var result = await sut.FindDownloadAsync(
            connection: source.Connection,
            kind: MediaKind.Episode,
            hash: source.Identity.DownloadHash,
            filename: source.Identity.OriginalFileName,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result!.EpisodeId.Should().Be(source.Identity.EpisodeId);
    }

    private static void PrepareMovie(WireScenario source)
    {
        source.Identity.Kind = MediaKind.Movie;
        var row = source.Queue.Records[0];
        row.DownloadId = source.Identity.DownloadHash;
        row.MovieId = source.Identity.MovieId;
        source.Queue.TotalRecords = source.Queue.Records.Count;
    }

    private static void PrepareEpisode(WireScenario source)
    {
        source.Identity.Kind = MediaKind.Episode;
        source.Identity.SeasonNumber %= 50;
        source.Identity.EpisodeNumber = source.Identity.EpisodeNumber % 90 + 1;
        source.Identity.OriginalFileName = $"{source.Identity.Title}.S{source.Identity.SeasonNumber:00}E{source.Identity.EpisodeNumber:00}.mkv";
        var row = source.Queue.Records[0];
        row.DownloadId = source.Identity.DownloadHash;
        row.SeriesId = source.Identity.SeriesId;
        source.Queue.TotalRecords = source.Queue.Records.Count;
        var episode = source.Episodes[0];
        episode.Id = source.Identity.EpisodeId;
        episode.SeasonNumber = source.Identity.SeasonNumber;
        episode.EpisodeNumber = source.Identity.EpisodeNumber;
    }

    private static void PrepareImport(WireScenario source)
    {
        source.File.MovieId = source.Identity.MovieId;
        source.File.SeriesId = source.Identity.SeriesId;
        source.File.Path = "/media/" + source.File.Path + ".mkv";
        var row = source.History.Records[0];
        row.DownloadId = source.Identity.DownloadHash;
        row.MovieId = source.Identity.MovieId;
        row.SeriesId = source.Identity.SeriesId;
        row.EpisodeId = source.Identity.EpisodeId;
        row.EventType = "downloadFolderImported";
        row.Data["droppedPath"] = "/downloads/" + source.Identity.OriginalFileName;
        row.Data["importedPath"] = source.File.Path;
        row.Data["size"] = source.File.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
        row.Data["fileId"] = source.File.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        source.History.TotalRecords = source.History.Records.Count;
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
            fixture.Register<IArrDownloadDiscovery>(() => fixture.Create<ArrDownloadDiscovery>());
            fixture.Register<IArrImportResolver>(() => fixture.Create<ArrImportResolver>());
            fixture.Customize<ArrConnection>(composer => composer.With(connection => connection.Url, "http://arr.invalid/"));
            fixture.Customize<MediaIdentity>(composer => composer.With(identity => identity.OriginalFileName, () => fixture.Create<string>() + ".mkv"));
            fixture.Register(() => new HttpClient(fixture.Create<TestHttpHandler>()));
            return fixture;
        }
    );
}
