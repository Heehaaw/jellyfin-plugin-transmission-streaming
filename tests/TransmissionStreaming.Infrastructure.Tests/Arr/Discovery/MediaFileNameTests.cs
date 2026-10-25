namespace TransmissionStreaming.Infrastructure.Tests.Arr.Discovery;

using FluentAssertions;
using global::TransmissionStreaming.Infrastructure.Arr.Discovery;
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

    [Theory, AutoMoqData]
    internal void Should_FilterSamplesByFilenameWithoutRejectingTheirDirectory(string name)
    {
        // Arrange
        var sample = name + "-SAMPLE.MKV";
        var underSampleFolder = "C:\\sample\\" + name + ".MKV";
        var ordinaryWord = name + "-sampled.mkv";

        // Act
        var sampleAccepted = MediaFileName.IsPlayable(sample);
        var nestedAccepted = MediaFileName.IsPlayable(underSampleFolder);
        var wordAccepted = MediaFileName.IsPlayable(ordinaryWord);

        // Assert
        sampleAccepted.Should().BeFalse();
        nestedAccepted.Should().BeTrue();
        wordAccepted.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal void Should_RefuseNonMediaFiles(string name)
    {
        // Arrange
        var filename = name + ".txt";

        // Act
        var accepted = MediaFileName.IsPlayable(filename);
        var episode = MediaFileName.ParseEpisode(filename);

        // Assert
        accepted.Should().BeFalse();
        episode.Should().BeNull();
    }
}
