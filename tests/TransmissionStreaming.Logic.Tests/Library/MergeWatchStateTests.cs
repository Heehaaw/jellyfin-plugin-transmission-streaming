namespace TransmissionStreaming.Logic.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Library;
using Xunit;

public sealed class MergeWatchStateTests
{
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        // Ratings remain inside the host-supported personal-rating range.
        fixture.Customize<LibraryWatchState>(c => c.With(x => x.Rating, () => fixture.Create<int>() % 11d));
        return fixture;
    });

    [Theory, AutoMoqData]
    internal async Task Should_UseTheNewerSourcePlayback(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(1);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.PlaybackPositionTicks.Should().Be(source.PlaybackPositionTicks);
        result.Played.Should().Be(source.Played);
        result.LastPlayedDate.Should().Be(source.LastPlayedDate);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepTheNewerTargetPlayback(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(-1);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
        result.Played.Should().Be(target.Played);
        result.LastPlayedDate.Should().Be(target.LastPlayedDate);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveManualWatchedStateAgainstAnUntouchedDuplicate(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = null;
        source.PlayCount = 0;
        source.PlaybackPositionTicks = 0;
        source.Played = false;
        target.LastPlayedDate = null;
        target.Played = true;
        target.PlayCount = 0;
        target.PlaybackPositionTicks = 0;

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Played.Should().Be(target.Played);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
        result.LastPlayedDate.Should().Be(target.LastPlayedDate);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveResumeAgainstAnUntouchedDuplicate(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = null;
        source.PlayCount = 0;
        source.PlaybackPositionTicks = 0;
        source.Played = false;
        target.LastPlayedDate = null;
        target.Played = false;

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
        result.Played.Should().Be(target.Played);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveConflictingUndatedManualState(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = null;
        source.Played = false;
        target.LastPlayedDate = null;
        target.Played = true;

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Played.Should().Be(target.Played);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotAssumeTimestampedPlaybackIsNewerThanAManualMark(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        target.LastPlayedDate = null;
        target.Played = true;
        source.Played = false;

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Played.Should().Be(target.Played);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RetainFavoritesFromAnOlderDuplicate(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(-1);
        source.IsFavorite = true;
        target.IsFavorite = false;

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFavorite.Should().Be(source.IsFavorite);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepTargetRatingWhenSourcePlaybackIsNewer(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(1);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Rating.Should().Be(target.Rating);
        result.Likes.Should().Be(target.Likes);
        result.PlaybackPositionTicks.Should().Be(source.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal async Task Should_UseSourceRatingWhenTargetHasNone(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        target.Rating = null;
        target.Likes = null;

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Rating.Should().Be(source.Rating);
        result.Likes.Should().Be(source.Likes);
    }

    [Theory, AutoMoqData]
    internal async Task Should_AvoidCountingTheSamePlaybackTwice(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        var target = query.Target!;
        var expectedCount = Math.Max(source.PlayCount, target.PlayCount);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.PlayCount.Should().Be(expectedCount);
    }

    [Theory, AutoMoqData]
    internal async Task Should_UseSourceWhenNoTargetExists(MergeWatchState.Query query, MergeWatchState.Handler sut)
    {
        // Arrange
        var source = query.Source;
        query = query with { Target = null };

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.PlaybackPositionTicks.Should().Be(source.PlaybackPositionTicks);
        result.Played.Should().Be(source.Played);
        result.LastPlayedDate.Should().Be(source.LastPlayedDate);
        result.Rating.Should().Be(source.Rating);
    }
}
