namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Logic.Configuration;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using Mediator;
using Moq;
using Xunit;

public sealed class ConfigurationInitializerTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PersistMissingLocalSettingsWithoutEnablingDiscovery(PluginConfiguration source, ConfigurationDefaults defaults,
        [Frozen] Mock<IPluginSettings> settings, [Frozen] Mock<IMediator> mediator, ConfigurationInitializer sut)
    {
        // Arrange
        source.Enabled = false;
        source.ProjectionRoot = "";
        source.LocalMediaRoot = "";
        var connection = source.TransmissionUrl;
        settings.Setup(value => value.Configuration).Returns(source);
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(defaults));

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        source.ProjectionRoot.Should().Be(defaults.ProjectionRoot);
        source.LocalMediaRoot.Should().Be(defaults.LocalMediaRoot);
        source.Enabled.Should().BeFalse();
        source.TransmissionUrl.Should().Be(connection);
        settings.Verify(value => value.UpdateConfiguration(source), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveSuppliedSettingsAlone(PluginConfiguration source,
        [Frozen] Mock<IPluginSettings> settings, [Frozen] Mock<IMediator> mediator, ConfigurationInitializer sut)
    {
        // Arrange
        settings.Setup(value => value.Configuration).Returns(source);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        mediator.Verify(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        settings.Verify(value => value.UpdateConfiguration(It.IsAny<PluginConfiguration>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepTheSuppliedProjectionWhenOnlyTheMediaRootIsMissing(PluginConfiguration source, ConfigurationDefaults defaults,
        [Frozen] Mock<IPluginSettings> settings, [Frozen] Mock<IMediator> mediator, ConfigurationInitializer sut)
    {
        // Arrange
        source.LocalMediaRoot = "";
        var projection = source.ProjectionRoot;
        settings.Setup(value => value.Configuration).Returns(source);
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(defaults));

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        source.ProjectionRoot.Should().Be(projection);
        source.LocalMediaRoot.Should().Be(defaults.LocalMediaRoot);
        settings.Verify(value => value.UpdateConfiguration(source), Times.Once);
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveAnUnknownMediaRootForTheAdministrator(PluginConfiguration source, ConfigurationDefaults defaults,
        [Frozen] Mock<IPluginSettings> settings, [Frozen] Mock<IMediator> mediator, ConfigurationInitializer sut)
    {
        // Arrange
        source.LocalMediaRoot = "";
        settings.Setup(value => value.Configuration).Returns(source);
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(defaults with { LocalMediaRoot = null }));

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        source.LocalMediaRoot.Should().BeEmpty();
        settings.Verify(value => value.UpdateConfiguration(It.IsAny<PluginConfiguration>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowHostStartupWhenLibraryDiscoveryFails(PluginConfiguration source, IOException failure,
        [Frozen] Mock<IPluginSettings> settings, [Frozen] Mock<IMediator> mediator, ConfigurationInitializer sut)
    {
        // Arrange
        source.LocalMediaRoot = "";
        settings.Setup(value => value.Configuration).Returns(source);
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<ConfigurationDefaults>(failure));

        // Act
        var action = () => sut.StartAsync(CancellationToken.None);

        // Assert
        await action.Should().NotThrowAsync();
        settings.Verify(value => value.UpdateConfiguration(It.IsAny<PluginConfiguration>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RespectHostShutdown(PluginConfiguration source,
        [Frozen] Mock<IPluginSettings> settings, [Frozen] Mock<IMediator> mediator, ConfigurationInitializer sut)
    {
        // Arrange
        source.LocalMediaRoot = "";
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();
        settings.Setup(value => value.Configuration).Returns(source);
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), shutdown.Token))
            .Returns(ValueTask.FromCanceled<ConfigurationDefaults>(shutdown.Token));

        // Act
        var action = () => sut.StartAsync(shutdown.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        settings.Verify(value => value.UpdateConfiguration(It.IsAny<PluginConfiguration>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_AllowHostStartupWhenSettingsCannotBeRead(IOException failure,
        [Frozen] Mock<IPluginSettings> settings, ConfigurationInitializer sut)
    {
        // Arrange
        settings.Setup(value => value.Configuration).Throws(failure);

        // Act
        var action = () => sut.StartAsync(CancellationToken.None);

        // Assert
        await action.Should().NotThrowAsync();
        settings.Verify(value => value.UpdateConfiguration(It.IsAny<PluginConfiguration>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
