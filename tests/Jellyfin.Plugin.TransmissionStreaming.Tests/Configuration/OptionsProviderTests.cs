namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using Moq;
using Xunit;

public sealed class OptionsProviderTests
{
    [Theory, AutoMoqData]
    internal void Should_MapTheCurrentConfiguration(PluginConfiguration source,
        [Frozen] Mock<IPluginSettings> settings, OptionsProvider sut)
    {
        // Arrange
        settings.Setup(value => value.Configuration).Returns(source);

        // Act
        var result = sut.Get();

        // Assert
        result.Should().BeEquivalentTo(source, options => options.ExcludingMissingMembers());
        result.StateDirectory.Should().Be(settings.Object.StateDirectory);
    }

    [Theory, AutoMoqData]
    internal void Should_ReturnIndependentSnapshotsAfterSettingsChange(PluginConfiguration source,
        [Frozen] Mock<IPluginSettings> settings, OptionsProvider sut)
    {
        // Arrange
        settings.Setup(value => value.Configuration).Returns(source);
        var originalInterval = source.PollSeconds;

        // Act
        var before = sut.Get();
        source.PollSeconds++;
        var after = sut.Get();

        // Assert
        before.PollSeconds.Should().Be(originalInterval);
        after.PollSeconds.Should().Be(source.PollSeconds);
        after.Should().NotBeSameAs(before);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
