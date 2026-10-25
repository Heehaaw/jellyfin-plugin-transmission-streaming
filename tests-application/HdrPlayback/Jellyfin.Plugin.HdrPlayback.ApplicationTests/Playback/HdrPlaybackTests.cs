namespace Jellyfin.Plugin.HdrPlayback.ApplicationTests.Playback;

using FluentAssertions;
using Hosting;
using Jellyfin.Plugin.HdrPlayback.ApplicationTests.Player;
using System.Net;
using System.Text.Json.Nodes;
using Testing;
using Xunit;

public sealed class HdrPlaybackTests
{
    [Theory, AutoMoqData]
    internal async Task Should_KeepNativeQualityMenuUsableWithHdrOption(HdrJellyfinFixture source, HdrPlayerBrowser sut)
    {
        // Arrange
        await using var owned = sut;

        // Act
        await sut.VerifyMenuAsync(source, TestContext.Current.CancellationToken);

        // Assert
        // Browser assertions exercise both desktop and phone layouts in the actual Jellyfin player.
    }

    [Theory, AutoMoqData]
    internal async Task Should_LoadBothPluginsAndExtendTheActualPlayerMenu(HdrJellyfinFixture source)
    {
        // Arrange
        var menuAsset = await source.PlayerMenuAssetAsync();

        // Act
        var plugins = (await source.JsonAsync("Plugins"))!.AsArray();
        var index = await source.TextAsync("web/index.html");
        var script = await source.TextAsync("HdrPlayback/player.js");
        var menu = await source.TextAsync("web/" + menuAsset);

        // Assert
        plugins.Should().Contain(plugin => plugin!["Name"]!.GetValue<string>() == "HDR Playback" && plugin["Status"]!.GetValue<string>() == "Active");
        plugins.Should().Contain(plugin => plugin!["Name"]!.GetValue<string>() == "Transmission Streaming" && plugin["Status"]!.GetValue<string>() == "Active");
        index.Should().Contain("/jf/HdrPlayback/player.js");
        script.Should().Contain("window.JellyfinHdrPlayback");
        menu.Should().Contain("JellyfinHdrPlayback.menu(").And.Contain("JellyfinHdrPlayback.toggle(");
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepOriginalNativeTranscodingAvailable(HdrJellyfinFixture source, byte bitrate)
    {
        // Arrange
        var maximum = (bitrate + 1) * 1000000;

        // Act
        using var response = await source.PlaybackAsync(false, maximum);
        var info = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        info["MediaSources"]!.AsArray().Should().ContainSingle();
        info["MediaSources"]![0]!["Id"]!.GetValue<string>().Should().Be(source.MovieId);
        info["MediaSources"]![0]!["TranscodingUrl"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        response.Headers.GetValues("X-Jellyfin-Hdr-Available").Should().ContainSingle().Which.Should().Be("false");
    }

    [Theory, AutoMoqData]
    internal async Task Should_ResolveConvertedVersionThroughNativeItemLookup(HdrJellyfinFixture source)
    {
        // Arrange
        var convertedId = "hdr-conversion:" + source.MovieId;

        // Act
        var item = await source.JsonAsync($"Users/{source.UserId}/Items/{convertedId}");

        // Assert
        item!["Id"]!.GetValue<string>().Should().Be(source.MovieId);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectHdrWhenServerHardwareIsNotConfigured(HdrJellyfinFixture source)
    {
        // Arrange
        const int maximum = 4000000;

        // Act
        using var response = await source.PlaybackAsync(true, maximum);
        var error = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error["title"]!.GetValue<string>().Should().Be("HDR conversion unavailable");
    }
}
