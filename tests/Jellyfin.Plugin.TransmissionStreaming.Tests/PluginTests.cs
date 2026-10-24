namespace Jellyfin.Plugin.TransmissionStreaming.Tests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;
using Xunit;
using HostPlugin = global::Jellyfin.Plugin.TransmissionStreaming.Plugin;

public sealed class PluginTests
{
    [Theory, AutoMoqData]
    internal void Should_PlaceItsStateBelowTheHostDataDirectory([Frozen] Mock<IApplicationPaths> paths, HostPlugin sut)
    {
        // Arrange
        var expected = Path.Combine(paths.Object.DataPath, PluginConstants.DataDirectory);

        // Act
        var result = sut.StateDirectory;

        // Assert
        result.Should().Be(expected);
        HostPlugin.Instance.Should().BeSameAs(sut);
    }

    [Theory, AutoMoqData]
    internal void Should_ExposeAnEmbeddedSettingsPageForTheSamePlugin(HostPlugin sut)
    {
        // Arrange
        var assembly = sut.GetType().Assembly;
        var expectedForm = "transmissionStreamingForm";

        // Act
        var page = sut.GetPages().Single();
        using var resource = assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
        using var reader = new StreamReader(resource!);
        var html = reader.ReadToEnd();

        // Assert
        page.Name.Should().Be(PluginConstants.SettingsPage);
        html.Should().Contain(sut.Id.ToString());
        html.Should().Contain(expectedForm);
    }

    [Theory, AutoMoqData]
    internal void Should_UseTheHostPluginForItsSettings(HostPlugin source)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPluginConfiguration();

        // Act
        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IPluginSettings>();
        var options = provider.GetRequiredService<IStreamingOptionsProvider>();

        // Assert
        settings.Should().BeSameAs(source);
        options.Should().BeOfType<OptionsProvider>();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<HostPlugin>(composer => composer.OmitAutoProperties());
        return fixture;
    });
}
