namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Hosting;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

public sealed class PlaybackFailureTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_BoundUnavailablePlaybackAndKeepTheServerResponsive(int requestedBytes)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var permanentUrl = await sut.WaitForStreamAsync();
        var hostUrl = sut.HostStream(permanentUrl);
        var missingOffset = sut.Swarm.PieceSize;
        var length = Math.Min(requestedBytes, sut.Swarm.PieceSize);
        using var missing = new HttpRequestMessage(HttpMethod.Get, hostUrl);
        missing.Headers.Range = new RangeHeaderValue(missingOffset, missingOffset + length - 1);
        using var invalid = new HttpRequestMessage(HttpMethod.Get, hostUrl);
        invalid.Headers.Range = new RangeHeaderValue(sut.ExpectedBytes.Length, null);

        try
        {
            // Act
            using var anonymousStatus = await client.GetAsync(new Uri(sut.Address, "/TransmissionStreaming/status"), cancellationToken);
            using var anonymousDefaults = await client.GetAsync(new Uri(sut.Address, "/TransmissionStreaming/configuration/defaults"), cancellationToken);
            using var noKey = await client.GetAsync(hostUrl.Split('?')[0], cancellationToken);
            using var invalidRange = await client.SendAsync(invalid, cancellationToken);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(700));
            using var abandoned = new HttpRequestMessage(HttpMethod.Get, hostUrl);
            abandoned.Headers.Range = missing.Headers.Range;
            var cancelled = false;
            try
            {
                using var ignored = await client.SendAsync(abandoned, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                cancelled = true;
            }

            using var stalled = await client.SendAsync(missing, cancellationToken);
            var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            var readers = JellyfinApi.Field(status, "Entries")!.AsArray().Select(entry => JellyfinApi.Value<int>(entry, "Readers"));
            await sut.ConfigureAsync(false);
            using var disabled = await client.GetAsync(hostUrl, cancellationToken);

            // Assert
            anonymousStatus.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            anonymousDefaults.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            noKey.StatusCode.Should().Be(HttpStatusCode.NotFound);
            invalidRange.StatusCode.Should().Be(HttpStatusCode.RequestedRangeNotSatisfiable);
            invalidRange.Content.Headers.ContentRange!.Length.Should().Be(sut.ExpectedBytes.Length);
            cancelled.Should().BeTrue();
            readers.Should().OnlyContain(count => count == 0, "cancelled and stalled requests must release their reader leases");
            stalled.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            JellyfinApi.Value<bool>(status, "Enabled").Should().BeTrue();
            disabled.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture();
            fixture.Customize(new AutoMoqCustomization());
            return fixture;
        }
    );
}
