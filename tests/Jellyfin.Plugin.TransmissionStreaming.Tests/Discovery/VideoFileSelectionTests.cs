namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using Jellyfin.Plugin.TransmissionStreaming.Discovery;
using Xunit;

public sealed class VideoFileSelectionTests
{
    [Theory, AutoMoqData]
    public void Should_SelectFeatureAfterEarlierExtra(TorrentSnapshot source, VideoFileSelection sut)
    {
        // Arrange
        var extra = source.Files[0];
        var feature = source.Files[1];
        extra.Name += ".mkv";
        feature.Name += ".mkv";
        feature.Length += extra.Length;
        var expectedIndex = source.Files.IndexOf(feature);

        // Act
        var result = sut.PrimaryMovieIndex(source.Files);

        // Assert
        result.Should().Be(expectedIndex);
    }

    [Theory, AutoMoqData]
    public void Should_RejectTorrentContainingOnlySamples(TorrentSnapshot source, VideoFileSelection sut)
    {
        // Arrange
        foreach (var file in source.Files) file.Name += ".sample.mkv";

        // Act
        var result = sut.PrimaryMovieIndex(source.Files);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    public void Should_ExcludeLargeTrailer(TorrentSnapshot source, VideoFileSelection sut)
    {
        // Arrange
        var trailer = source.Files[0];
        var feature = source.Files[1];
        trailer.Name += ".trailer.mkv";
        feature.Name += ".mkv";
        trailer.Length += feature.Length;
        var expectedIndex = source.Files.IndexOf(feature);

        // Act
        var result = sut.PrimaryMovieIndex(source.Files);

        // Assert
        result.Should().Be(expectedIndex);
    }

    [Theory, AutoMoqData]
    public void Should_RejectUnsupportedExtension(string source, VideoFileSelection sut)
    {
        // Arrange
        var filename = source + ".txt";

        // Act
        var result = sut.IsPlayable(filename);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    public void Should_ExcludeTrailerFolders(string source, VideoFileSelection sut)
    {
        // Arrange
        var filename = "Trailers/" + source + ".mkv";

        // Act
        var result = sut.IsPlayable(filename);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    public void Should_AcceptIndependentEpisodeFile(string source, VideoFileSelection sut)
    {
        // Arrange
        var filename = source + ".S01E02.mkv";

        // Act
        var result = sut.IsPlayable(filename);

        // Assert
        result.Should().BeTrue();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 2 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
