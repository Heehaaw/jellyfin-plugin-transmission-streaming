namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public sealed class PluginOptionsChangeTokenSourceTests
{
    [Theory, AutoMoqData]
    internal void Should_RebuildOptionsOnHostSaveAndUnsubscribeOnDisposal(PluginConfiguration source, ServiceCollection services,
        [Frozen] Mock<IPluginSettings> settings, ConfigureStreamingOptions configure, PluginOptionsChangeTokenSource sut)
    {
        // Arrange
        using var changes = sut;
        services.Clear();
        services.AddOptions<StreamingOptions>();
        services.AddSingleton<IConfigureOptions<StreamingOptions>>(configure);
        services.AddSingleton<IOptionsChangeTokenSource<StreamingOptions>>(sut);
        settings.SetupGet(value => value.Configuration).Returns(source);
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<StreamingOptions>>();
        var initial = monitor.CurrentValue;
        var initialInterval = initial.PollSeconds;
        var initialRoot = initial.PathMappings[0].LocalRoot;
        var observed = new List<StreamingOptions>();
        using var subscription = monitor.OnChange((value, _) => observed.Add(value));

        // Act
        source.PollSeconds++;
        source.PathMappings[0].LocalRoot += "/changed";
        var beforeSave = monitor.CurrentValue;
        settings.Raise(value => value.ConfigurationChanged += null, settings.Object, source);
        var updated = monitor.CurrentValue;
        sut.Dispose();
        source.PollSeconds++;
        settings.Raise(value => value.ConfigurationChanged += null, settings.Object, source);
        var afterDisposal = monitor.CurrentValue;

        // Assert
        beforeSave.Should().BeSameAs(initial);
        initial.PollSeconds.Should().Be(initialInterval);
        initial.PathMappings[0].LocalRoot.Should().Be(initialRoot);
        updated.Should().NotBeSameAs(initial);
        updated.PollSeconds.Should().Be(source.PollSeconds - 1);
        updated.PathMappings[0].LocalRoot.Should().Be(source.PathMappings[0].LocalRoot);
        observed.Should().ContainSingle().Which.Should().BeSameAs(updated);
        afterDisposal.Should().BeSameAs(updated);
        settings.VerifyRemove(value => value.ConfigurationChanged -= It.IsAny<EventHandler<BasePluginConfiguration>>(), Times.Once);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<ServiceCollection>(composer => composer.OmitAutoProperties());
        return fixture;
    });
}
