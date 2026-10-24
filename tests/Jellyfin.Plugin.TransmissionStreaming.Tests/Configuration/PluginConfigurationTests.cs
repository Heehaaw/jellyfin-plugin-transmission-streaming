namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using Xunit;

public sealed class PluginConfigurationTests
{
    [Theory, AutoMoqData]
    internal void Should_RequireExplicitEnablement(PluginConfiguration sut)
    {
        // Arrange
        var disabled = false;

        // Act
        var result = sut.Enabled;

        // Assert
        result.Should().Be(disabled);
    }

    [Theory, AutoMoqData]
    internal void Should_DefaultToBoundedProgressivePlayback(PluginConfiguration sut)
    {
        // Arrange
        var enabled = true;
        var minimumInterval = 2;
        var maximumInterval = 300;
        var minimumStall = 5;
        var maximumStall = 600;

        // Act
        var prioritize = sut.PrioritizePlayback;
        var interval = sut.PollSeconds;
        var stall = sut.PieceWaitSeconds;

        // Assert
        prioritize.Should().Be(enabled);
        interval.Should().BeInRange(minimumInterval, maximumInterval);
        stall.Should().BeInRange(minimumStall, maximumStall);
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveConnectionSecretsEmpty(PluginConfiguration sut)
    {
        // Arrange
        var empty = string.Empty;

        // Act
        var secrets = new[] { sut.TransmissionUsername, sut.TransmissionPassword, sut.RadarrApiKey, sut.SonarrApiKey };

        // Assert
        secrets.Should().OnlyContain(value => value == empty);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<PluginConfiguration>(composer => composer.OmitAutoProperties());
        return fixture;
    });
}
