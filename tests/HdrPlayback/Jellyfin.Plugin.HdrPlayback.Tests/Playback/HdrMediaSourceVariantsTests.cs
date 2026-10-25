namespace Jellyfin.Plugin.HdrPlayback.Tests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.HdrPlayback.Playback;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Xunit;

public sealed class HdrMediaSourceVariantsTests
{
    [Theory, AutoMoqData]
    internal void Should_AddAConversionChoiceWithoutChangingTheOriginal(MediaSourceInfo source)
    {
        // Arrange
        source.MediaStreams[0].Type = MediaStreamType.Video;
        source.MediaStreams[0].ColorTransfer = "bt709";
        source.SupportsTranscoding = true;

        // Act
        var choices = HdrMediaSourceVariants.AddChoices([source]);
        var converted = choices.Last();

        // Assert
        choices.Should().HaveCount(2);
        choices.First().Should().BeSameAs(source);
        converted.Id.Should().Be(HdrMediaSourceVariants.Prefix + source.Id);
        converted.Path.Should().Be(source.Path);
        converted.VideoStream.ColorTransfer.Should().Be(source.VideoStream.ColorTransfer);
        converted.MediaStreams.Should().NotBeSameAs(source.MediaStreams);
        converted.SupportsDirectPlay.Should().BeFalse();
        converted.SupportsDirectStream.Should().BeFalse();
        converted.TranscodingUrl.Should().BeNull();
        converted.Name.Should().Contain(source.Name).And.Contain(HdrMediaSourceVariants.Label);
    }

    [Theory, AutoMoqData]
    internal void Should_NotOfferConversionForNativeHdr(MediaSourceInfo source)
    {
        // Arrange
        source.MediaStreams[0].Type = MediaStreamType.Video;
        source.MediaStreams[0].ColorTransfer = "smpte2084";
        source.SupportsTranscoding = true;

        // Act
        var choices = HdrMediaSourceVariants.AddChoices([source]);

        // Assert
        choices.Should().ContainSingle().Which.Should().BeSameAs(source);
    }

    [Theory, AutoMoqData]
    internal void Should_NotDuplicateConversionChoices(MediaSourceInfo source)
    {
        // Arrange
        source.MediaStreams[0].Type = MediaStreamType.Video;
        source.MediaStreams[0].ColorTransfer = "bt709";
        source.SupportsTranscoding = true;
        var choices = HdrMediaSourceVariants.AddChoices([source]);

        // Act
        var repeated = HdrMediaSourceVariants.AddChoices(choices);

        // Assert
        repeated.Select(choice => choice.Id).Should().Equal(choices.Select(choice => choice.Id));
    }

    [Theory, AutoMoqData]
    internal void Should_ResolveOnlyAConversionChoice(string source)
    {
        // Arrange
        var selected = HdrMediaSourceVariants.Prefix + source;

        // Act
        var original = HdrMediaSourceVariants.OriginalId(selected);

        // Assert
        original.Should().Be(source);
        HdrMediaSourceVariants.OriginalId(source).Should().BeNull();
        HdrMediaSourceVariants.OriginalId(HdrMediaSourceVariants.Prefix).Should().BeNull();
        HdrMediaSourceVariants.OriginalId(HdrMediaSourceVariants.Prefix + selected).Should().BeNull();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
