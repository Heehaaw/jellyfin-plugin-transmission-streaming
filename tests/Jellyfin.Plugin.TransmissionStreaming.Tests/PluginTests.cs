namespace Jellyfin.Plugin.TransmissionStreaming.Tests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using MediaBrowser.Common.Configuration;
using Moq;
using System.Text.RegularExpressions;
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
        var page = sut.GetPages().Single(page => page.Name == PluginConstants.SettingsPage);
        using var resource = assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
        using var reader = new StreamReader(resource!);
        var html = reader.ReadToEnd();

        // Assert
        page.Name.Should().Be(PluginConstants.SettingsPage);
        html.Should().Contain(sut.Id.ToString());
        html.Should().Contain(expectedForm);
        html.Should().Contain("data-controller=\"__plugin/" + PluginConstants.Ui.Controller + "\"");
        html.Should().NotContain("<script>");
    }

    [Theory, AutoMoqData]
    internal void Should_EmbedEveryAdvertisedAssetAndRegisterItsModuleDependencies(HostPlugin sut)
    {
        // Arrange
        var pages = sut.GetPages().ToDictionary(page => page.Name);
        var assembly = sut.GetType().Assembly;
        var advertisedNames = pages.Keys;

        // Act
        var dependencies = new List<string>();
        foreach (var page in pages.Values)
        {
            using var resource = assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
            using var reader = new StreamReader(resource!);
            var content = reader.ReadToEnd();
            dependencies.AddRange(Regex.Matches(content, @"\./ConfigurationPage\?name=([^'""\s<>]+)").Select(match => match.Groups[1].Value));
        }

        // Assert
        dependencies.Should().NotBeEmpty().And.OnlyContain(name => advertisedNames.Contains(name));
        advertisedNames.Should().Contain(PluginConstants.Ui.Controller).And.Contain(PluginConstants.Ui.Styles);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<HostPlugin>(composer => composer.OmitAutoProperties());
        return fixture;
    });
}
