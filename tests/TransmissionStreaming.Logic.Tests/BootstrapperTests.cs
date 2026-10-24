namespace TransmissionStreaming.Logic.Tests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransmissionStreaming.Logic;
using TransmissionStreaming.Logic.Discovery;
using Xunit;

public sealed class BootstrapperTests
{
    [Theory, AutoMoqData]
    internal void Should_ComposeTheDiscoveryFeature(ServiceCollection source)
    {
        // Arrange
        source.Clear();

        // Act
        var result = source.AddTransmissionStreamingLogic();
        using var provider = source.BuildServiceProvider();
        var selection = provider.GetRequiredService<IVideoFileSelection>();

        // Assert
        result.Should().BeSameAs(source);
        selection.Should().BeOfType<VideoFileSelection>();
        provider.GetRequiredService<IVideoFileSelection>().Should().BeSameAs(selection);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture();
        fixture.Customize(new AutoMoqCustomization());
        // A service collection is infrastructure, and must start with real descriptors only.
        fixture.Customize<ServiceCollection>(composer => composer.OmitAutoProperties());
        return fixture;
    });
}
