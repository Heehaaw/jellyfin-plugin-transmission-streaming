namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Hosting;
using System.Net;
using Xunit;

public sealed class OrphanCleanupTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_RemoveACancelledMovieFromTrackingAndJellyfinAndKeepItGoneAfterRestart(string libraryName)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var permanentUrl = await sut.WaitForStreamAsync();
        await sut.AddLibraryAsync(libraryName, sut.ProjectionMoviesPath);
        await sut.WaitForItemAsync();
        var localProjection = Directory.EnumerateFiles(Path.Combine(sut.Workspace, "projection"), "*.strm", SearchOption.AllDirectories).Single();
        var sidecar = Path.ChangeExtension(localProjection, ".nfo");

        try
        {
            // Act
            await sut.Arr.SetRemovedAsync(true, cancellationToken);
            await sut.Swarm.RemoveAsync(cancellationToken);
            File.Delete(sut.Swarm.LocalPartialPath);
            await JellyfinTestServer.UntilAsync(
                probe: async () =>
                {
                    var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
                    return JellyfinApi.Field(status, "Entries")!.AsArray().Count == 0 &&
                           !File.Exists(localProjection) && await sut.FindItemAsync(true) is null;
                },
                description: "orphan projection and library cleanup"
            );
            await sut.RestartAsync();
            var afterRestart = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            var item = await sut.FindItemAsync(true);
            using var client = new HttpClient();
            using var oldStream = await client.GetAsync(sut.HostStream(permanentUrl), cancellationToken);

            // Assert
            JellyfinApi.Field(afterRestart, "Entries")!.AsArray().Should().BeEmpty();
            item.Should().BeNull();
            File.Exists(localProjection).Should().BeFalse();
            File.Exists(sidecar).Should().BeFalse();
            File.Exists(Path.Combine(sut.Workspace, "reference.mkv")).Should().BeTrue();
            oldStream.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private sealed class AutoMoqDataAttribute() : ApplicationDataAttribute(() =>
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
            return fixture;
        }
    );
}
