namespace TransmissionStreaming.Infrastructure.Tests.Transmission;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure.Transmission;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class TransmissionClientTests
{
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
            fixture.Register(() => new HttpClient(fixture.Freeze<ScriptedHandler>()));
            fixture.Customize<TransmissionConnection>(c =>
                c.FromFactory((string user, string password) => new TransmissionConnection("http://localhost:9091/transmission/rpc", user, password)).OmitAutoProperties()
            );
            return fixture;
        }
    );

    [Theory, AutoMoqData]
    internal async Task Should_NegotiateSessionAndReadSnakeCaseFields(
        TransmissionConnection connection,
        TorrentSnapshot torrent,
        string sessionId,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        var requests = 0;
        string? method = null;
        string? receivedSession = null;
        handler.Respond = async request =>
        {
            requests++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            method = payload.RootElement.GetProperty("method").GetString();
            if (requests == 1)
            {
                var conflict = new HttpResponseMessage(HttpStatusCode.Conflict);
                conflict.Headers.Add("X-Transmission-Session-Id", sessionId);
                return conflict;
            }

            receivedSession = request.Headers.GetValues("X-Transmission-Session-Id").Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            jsonrpc = "2.0",
                            id = 1,
                            result = new
                            {
                                torrents = new[]
                                {
                                    torrent
                                }
                            }
                        }
                    )
                )
            };
        };
        var expectedMethod = "torrent_get";

        // Act
        var result = await sut.GetTorrentsAsync(connection);

        // Assert
        requests.Should().Be(2);
        receivedSession.Should().Be(sessionId);
        method.Should().Be(expectedMethod);
        result.Single().Hash.Should().Be(torrent.Hash);
        result.Single().Files.Single().Length.Should().Be(torrent.Files.Single().Length);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectRedirectResponses(
        TransmissionConnection connection,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect));

        // Act
        var action = () => sut.GetTorrentsAsync(connection);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReadSessionCacheAndIncompleteDirectorySettings(
        TransmissionConnection connection,
        TransmissionSessionSnapshot source,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        var authenticated = false;
        handler.Respond = request =>
        {
            var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(connection.Username + ":" + connection.Password));
            authenticated = request.Headers.Authorization?.Scheme == "Basic" && request.Headers.Authorization.Parameter == expected;
            return Task.FromResult(RpcResponse(source));
        };

        // Act
        var session = await sut.GetSessionAsync(connection);

        // Assert
        authenticated.Should().BeTrue("RPC credentials must be scoped to the configured endpoint");
        session.Should().BeEquivalentTo(source);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SelectOnlyTheRequestedTorrent(
        TransmissionConnection connection,
        TorrentSnapshot source,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        JsonElement parameters = default;
        handler.Respond = async request =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            parameters = payload.RootElement.GetProperty("params").Clone();
            return RpcResponse(
                new
                {
                    torrents = new[]
                    {
                        source
                    }
                }
            );
        };

        // Act
        var torrent = await sut.GetTorrentAsync(connection, source.Hash);

        // Assert
        parameters.GetProperty("ids").EnumerateArray().Single().GetString().Should().Be(source.Hash);
        torrent!.Hash.Should().Be(source.Hash);
    }

    [Theory, AutoMoqData]
    internal async Task Should_EnableSequentialDownloadingOnlyForThePlayedTorrent(
        TransmissionConnection connection,
        TorrentSnapshot source,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        JsonElement parameters = default;
        handler.Respond = async request =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            parameters = payload.RootElement.GetProperty("params").Clone();
            return RpcResponse(
                new
                {
                }
            );
        };

        // Act
        await sut.EnableSequentialAsync(connection, source.Hash);

        // Assert
        parameters.GetProperty("ids").EnumerateArray().Single().GetString().Should().Be(source.Hash);
        parameters.GetProperty("sequential_download").GetBoolean().Should().BeTrue();
        parameters.EnumerateObject().Should().HaveCount(2, "global cache or unrelated torrent settings must not change");
    }

    [Theory, AutoMoqData]
    internal async Task Should_MoveTheCursorForOnlyTheSelectedTorrent(
        TransmissionConnection connection,
        TorrentSnapshot source,
        byte piece,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        JsonElement parameters = default;
        handler.Respond = async request =>
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            parameters = payload.RootElement.GetProperty("params").Clone();
            return RpcResponse(
                new
                {
                }
            );
        };

        // Act
        await sut.SetSequentialStartAsync(connection, source.Hash, piece);

        // Assert
        parameters.GetProperty("ids").EnumerateArray().Single().GetString().Should().Be(source.Hash);
        parameters.GetProperty("sequential_download_from_piece").GetInt32().Should().Be(piece);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SurfaceRpcRejectionWithoutItsMessage(
        TransmissionConnection connection,
        string upstreamMessage,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        handler.Respond = _ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            error = upstreamMessage
                        }
                    )
                )
            }
        );

        // Act
        var action = () => sut.GetSessionAsync(connection);

        // Assert
        var failure = await action.Should().ThrowAsync<IOException>();
        failure.Which.Message.Contains(upstreamMessage, StringComparison.Ordinal).Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectInvalidRpcResultShapes(
        TransmissionConnection connection,
        [Frozen] ScriptedHandler handler,
        TransmissionClient sut
    )
    {
        // Arrange
        handler.Respond = _ => Task.FromResult(RpcResponse(Array.Empty<string>()));

        // Act
        var action = () => sut.GetSessionAsync(connection);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }

    private static HttpResponseMessage RpcResponse<T>(T result)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        result
                    }
                )
            )
        };
    }
}
