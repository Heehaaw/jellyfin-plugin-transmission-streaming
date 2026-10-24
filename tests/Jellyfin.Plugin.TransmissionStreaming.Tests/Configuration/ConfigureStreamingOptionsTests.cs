namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using Moq;
using Xunit;

public sealed class ConfigureStreamingOptionsTests
{
    [Theory, AutoMoqData]
    internal void Should_MapTheCurrentConfiguration(PluginConfiguration source, StreamingOptions target,
        [Frozen] Mock<IPluginSettings> settings, ConfigureStreamingOptions sut)
    {
        // Arrange
        settings.SetupGet(value => value.Configuration).Returns(source);

        // Act
        sut.Configure(target);

        // Assert
        target.Should().BeEquivalentTo(source, options => options.ExcludingMissingMembers());
        target.StateDirectory.Should().Be(settings.Object.StateDirectory);
    }

    [Theory, AutoMoqData]
    internal void Should_KeepPreviousSnapshotsIndependentOfHostSettings(PluginConfiguration source, StreamingOptions before, StreamingOptions after,
        [Frozen] Mock<IPluginSettings> settings, ConfigureStreamingOptions sut)
    {
        // Arrange
        settings.SetupGet(value => value.Configuration).Returns(source);
        var originalInterval = source.PollSeconds;
        var originalLocalRoot = source.PathMappings[0].LocalRoot;

        // Act
        sut.Configure(before);
        source.PollSeconds++;
        source.PathMappings[0].LocalRoot += "/changed";
        sut.Configure(after);

        // Assert
        before.PollSeconds.Should().Be(originalInterval);
        before.PathMappings[0].LocalRoot.Should().Be(originalLocalRoot);
        after.PathMappings[0].LocalRoot.Should().Be(source.PathMappings[0].LocalRoot);
        after.PollSeconds.Should().Be(source.PollSeconds);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
