namespace Jellyfin.Plugin.HdrPlayback.Tests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.HdrPlayback.Playback;
using MediaBrowser.Model.Dlna;
using Xunit;

public sealed class HdrCapabilitiesTests
{
    [Theory, AutoMoqData]
    internal void Should_AcceptDeclaredHdr10Playback(DeviceProfile source)
    {
        // Arrange
        var codec = PrepareHevc(source);
        codec.Conditions[0].Property = ProfileConditionValue.VideoRangeType;
        codec.Conditions[0].Value = "SDR|HDR10";

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal void Should_AcceptNativeMain10ProfileWithoutRangeField(DeviceProfile source)
    {
        // Arrange
        PrepareHevc(source);

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal void Should_AcceptNativeHdr10ProfileNames(DeviceProfile source)
    {
        // Arrange
        var codec = PrepareHevc(source);
        foreach (var condition in codec.Conditions)
        {
            condition.Value = "Main 10 HDR 10|Main 10 HDR 10 Plus";
        }

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectMainOnlyWithoutRangeDeclaration(DeviceProfile source)
    {
        // Arrange
        var codec = PrepareHevc(source);
        foreach (var condition in codec.Conditions)
        {
            condition.Value = "main";
        }

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_HonorExplicitSdrOnlyRestriction(DeviceProfile source)
    {
        // Arrange
        var codec = PrepareHevc(source);
        codec.Conditions[1].Property = ProfileConditionValue.VideoRangeType;
        codec.Conditions[1].Value = "SDR";

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_HonorExplicitHdr10Exclusion(DeviceProfile source)
    {
        // Arrange
        var codec = PrepareHevc(source);
        codec.Conditions[1].Property = ProfileConditionValue.VideoRangeType;
        codec.Conditions[1].Condition = ProfileConditionType.NotEquals;
        codec.Conditions[1].Value = "HDR10";

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectOtherCodecs(DeviceProfile source)
    {
        // Arrange
        var codec = PrepareHevc(source);
        codec.Codec = "h264";

        // Act
        var supported = HdrCapabilities.ClientSupportsHdr(source);

        // Assert
        supported.Should().BeFalse();
    }

    private static CodecProfile PrepareHevc(DeviceProfile source)
    {
        var codec = source.CodecProfiles[0];
        source.CodecProfiles = [codec];
        codec.Type = CodecType.Video;
        codec.Codec = "hevc";
        foreach (var condition in codec.Conditions)
        {
            condition.Property = ProfileConditionValue.VideoProfile;
            condition.Condition = ProfileConditionType.EqualsAny;
            condition.Value = "main|main 10";
        }

        return codec;
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 2 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
