namespace TransmissionStreaming.IntegrationTests.Host.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using HostPlugin = global::Jellyfin.Plugin.TransmissionStreaming.Plugin;

public sealed class BootstrapperTests
{
    [Theory, AutoMoqData]
    internal void Should_UseTheHostPluginForItsSettings(HostPlugin source)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPluginConfiguration();

        // Act
        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IPluginSettings>();
        var options = provider.GetRequiredService<IOptionsMonitor<StreamingOptions>>();

        // Assert
        settings.Should().BeSameAs(source);
        options.Should().BeOfType<OptionsMonitor<StreamingOptions>>();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            fixture.Customize<HostPlugin>(composer => composer.OmitAutoProperties());
            return fixture;
        }
    );
}
