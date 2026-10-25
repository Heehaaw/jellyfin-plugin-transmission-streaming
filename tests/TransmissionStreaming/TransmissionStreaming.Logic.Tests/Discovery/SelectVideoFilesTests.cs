namespace TransmissionStreaming.Logic.Tests.Discovery;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using TransmissionStreaming.Logic.Discovery;
using Xunit;

public sealed class SelectVideoFilesTests
{
    [Theory, AutoMoqData]
    internal async Task Should_SelectTheLargestEligibleMovie(SelectVideoFiles.Query source, SelectVideoFiles.Handler sut)
    {
        // Arrange
        var extra = source.Files[0];
        var feature = source.Files[1];
        extra.Name += ".mkv";
        feature.Name += ".mov";
        feature.Length += extra.Length;
        var expectedIndex = source.Files.ToList().IndexOf(feature);

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.PrimaryMovieIndex.Should().Be(expectedIndex);
        result.PlayableIndexes.Should().HaveCount(source.Files.Count);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectSamplesAndTrailers(SelectVideoFiles.Query source, SelectVideoFiles.Handler sut)
    {
        // Arrange
        source.Files[0].Name += ".sample.mkv";
        source.Files[1].Name = "Trailers/" + source.Files[1].Name + ".mkv";

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.PlayableIndexes.Should().BeEmpty();
        result.PrimaryMovieIndex.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ExcludeALargerTrailer(SelectVideoFiles.Query source, SelectVideoFiles.Handler sut)
    {
        // Arrange
        var trailer = source.Files[0];
        var feature = source.Files[1];
        trailer.Name += ".trailer.mkv";
        feature.Name += ".mkv";
        trailer.Length += feature.Length;
        var expectedIndex = source.Files.ToList().IndexOf(feature);

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.PlayableIndexes.Should().Equal(expectedIndex);
        result.PrimaryMovieIndex.Should().Be(expectedIndex);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectUnsupportedPayloads(SelectVideoFiles.Query source, SelectVideoFiles.Handler sut)
    {
        // Arrange
        foreach (var file in source.Files)
        {
            file.Name += ".txt";
        }

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.PlayableIndexes.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal async Task Should_ExposeEveryIndependentEpisode(SelectVideoFiles.Query source, SelectVideoFiles.Handler sut)
    {
        // Arrange
        foreach (var file in source.Files)
        {
            file.Name += ".S01E02.mkv";
        }

        var expected = Enumerable.Range(0, source.Files.Count);

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.PlayableIndexes.Should().Equal(expected);
    }

    [Theory, AutoMoqData]
    internal async Task Should_IgnoreEmptyFiles(SelectVideoFiles.Query source, SelectVideoFiles.Handler sut)
    {
        // Arrange
        foreach (var file in source.Files)
        {
            file.Name += ".mkv";
        }

        source.Files[0].Length = 0;
        var expectedIndex = source.Files.ToList().IndexOf(source.Files[1]);

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.PlayableIndexes.Should().Equal(expectedIndex);
        result.PrimaryMovieIndex.Should().Be(expectedIndex);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 2
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
