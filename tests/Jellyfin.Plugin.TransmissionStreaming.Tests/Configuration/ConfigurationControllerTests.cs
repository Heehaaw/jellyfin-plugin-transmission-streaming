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

public sealed class ConfigurationControllerTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReturnTheInferredDefaults(ConfigurationDefaults source,
        [Frozen] Mock<IMediator> mediator, ConfigurationController sut)
    {
        // Arrange
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source));

        // Act
        var result = await sut.Defaults(CancellationToken.None);

        // Assert
        result.Value.Should().BeSameAs(source);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<ConfigurationController>(composer => composer.OmitAutoProperties());
        return fixture;
    });
}
