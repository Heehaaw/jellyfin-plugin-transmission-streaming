namespace TransmissionStreaming.Infrastructure.Tests.Transmission;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure.Transmission;
using System.Net;
using System.Text.Json;
using Xunit;

public sealed class TransmissionClientTests
{
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Register(() => new HttpClient(fixture.Freeze<ScriptedHandler>()));
        fixture.Customize<TransmissionConnection>(c => c.FromFactory((string user, string password) => new TransmissionConnection("http://localhost:9091/transmission/rpc", user, password)).OmitAutoProperties());
        return fixture;
    });

    [Theory, AutoMoqData]
    internal async Task Should_NegotiateSessionAndReadSnakeCaseFields(
        TransmissionConnection connection, TorrentSnapshot torrent, string sessionId,
        [Frozen] ScriptedHandler handler, TransmissionClient sut)
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
                Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, result = new { torrents = new[] { torrent } } }))
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
        TransmissionConnection connection, [Frozen] ScriptedHandler handler, TransmissionClient sut)
    {
        // Arrange
        handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect));

        // Act
        var action = () => sut.GetTorrentsAsync(connection);

        // Assert
        await action.Should().ThrowAsync<IOException>();
    }
}
