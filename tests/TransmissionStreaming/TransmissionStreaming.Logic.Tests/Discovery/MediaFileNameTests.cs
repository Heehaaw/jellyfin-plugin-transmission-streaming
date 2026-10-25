namespace TransmissionStreaming.Logic.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using TransmissionStreaming.Logic.Discovery;
using Xunit;

public sealed class MediaFileNameTests
{
    [Theory, AutoMoqData]
    internal void Should_RecognizeBothConventionalEpisodeFormats(string name, byte seasonSeed, byte episodeSeed)
    {
        // Arrange
        var season = seasonSeed % 100;
        var episode = episodeSeed + 1;
        var standard = $"{name}.S{season:00}E{episode:00}.mkv";
        var alternate = $"{name}.{season}x{episode:00}.mkv";

        // Act
        var first = MediaFileName.ParseEpisode(standard);
        var second = MediaFileName.ParseEpisode(alternate);

        // Assert
        first.Should().NotBeNull();
        first!.Value.Season.Should().Be(season);
        first.Value.Episode.Should().Be(episode);
        second.Should().Be(first);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAmbiguousEpisodeMappings(string name)
    {
        // Arrange
        var combined = name + ".S01E02E03.mkv";
        var twoMatches = name + ".S01E02.S01E03.mkv";

        // Act
        var combinedResult = MediaFileName.ParseEpisode(combined);
        var multipleResult = MediaFileName.ParseEpisode(twoMatches);

        // Assert
        combinedResult.Should().BeNull();
        multipleResult.Should().BeNull();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
