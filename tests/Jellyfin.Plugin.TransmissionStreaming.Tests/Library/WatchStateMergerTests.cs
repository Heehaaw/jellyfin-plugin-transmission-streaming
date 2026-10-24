namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using MediaBrowser.Controller.Entities;
using Xunit;

public sealed class WatchStateMergerTests
{
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        // Jellyfin validates ratings, and assigning Likes would overwrite the generated rating.
        fixture.Customize<UserItemData>(c => c.With(x => x.Rating, () => fixture.Create<int>() % 11d).Without(x => x.Likes));
        return fixture;
    });

    [Theory, AutoMoqData]
    internal void Should_UseTheNewerSourcePlayback(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(1);

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.PlaybackPositionTicks.Should().Be(source.PlaybackPositionTicks);
        result.Played.Should().Be(source.Played);
        result.LastPlayedDate.Should().Be(source.LastPlayedDate);
    }

    [Theory, AutoMoqData]
    internal void Should_KeepTheNewerTargetPlayback(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(-1);

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
        result.Played.Should().Be(target.Played);
        result.LastPlayedDate.Should().Be(target.LastPlayedDate);
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveManualWatchedStateAgainstAnUntouchedDuplicate(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = null;
        source.PlayCount = 0;
        source.PlaybackPositionTicks = 0;
        source.Played = false;
        target.LastPlayedDate = null;
        target.Played = true;
        target.PlayCount = 0;
        target.PlaybackPositionTicks = 0;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.Played.Should().Be(target.Played);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
        result.LastPlayedDate.Should().Be(target.LastPlayedDate);
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveResumeAgainstAnUntouchedDuplicate(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = null;
        source.PlayCount = 0;
        source.PlaybackPositionTicks = 0;
        source.Played = false;
        target.LastPlayedDate = null;
        target.Played = false;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
        result.Played.Should().Be(target.Played);
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveConflictingUndatedManualState(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = null;
        source.Played = false;
        target.LastPlayedDate = null;
        target.Played = true;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.Played.Should().Be(target.Played);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal void Should_NotAssumeTimestampedPlaybackIsNewerThanAManualMark(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        target.LastPlayedDate = null;
        target.Played = true;
        source.Played = false;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.Played.Should().Be(target.Played);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal void Should_RetainFavoritesFromAnOlderDuplicate(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(-1);
        source.IsFavorite = true;
        target.IsFavorite = false;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.IsFavorite.Should().Be(source.IsFavorite);
        result.PlaybackPositionTicks.Should().Be(target.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal void Should_KeepTargetRatingWhenSourcePlaybackIsNewer(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        source.LastPlayedDate = target.LastPlayedDate.GetValueOrDefault().AddMinutes(1);

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.Rating.Should().Be(target.Rating);
        result.Likes.Should().Be(target.Likes);
        result.PlaybackPositionTicks.Should().Be(source.PlaybackPositionTicks);
    }

    [Theory, AutoMoqData]
    internal void Should_UseSourceRatingWhenTargetHasNone(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        target.Rating = null;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.Rating.Should().Be(source.Rating);
        result.Likes.Should().Be(source.Likes);
    }

    [Theory, AutoMoqData]
    internal void Should_AvoidCountingTheSamePlaybackTwice(UserItemData source, UserItemData target, WatchStateMerger sut)
    {
        // Arrange
        var expectedCount = Math.Max(source.PlayCount, target.PlayCount);

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.PlayCount.Should().Be(expectedCount);
    }

    [Theory, AutoMoqData]
    internal void Should_UseSourceWhenNoTargetExists(UserItemData source, WatchStateMerger sut)
    {
        // Arrange
        UserItemData? target = null;

        // Act
        var result = sut.Merge(source, target);

        // Assert
        result.PlaybackPositionTicks.Should().Be(source.PlaybackPositionTicks);
        result.Played.Should().Be(source.Played);
        result.LastPlayedDate.Should().Be(source.LastPlayedDate);
        result.Rating.Should().Be(source.Rating);
    }
}
