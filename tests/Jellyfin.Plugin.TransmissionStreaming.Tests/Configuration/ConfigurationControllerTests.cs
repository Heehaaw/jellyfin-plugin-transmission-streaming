namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Logic.Configuration;
using Jellyfin.Plugin.TransmissionStreaming.Configuration;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

public sealed class ConfigurationControllerTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReturnTheInferredDefaults(
        ConfigurationDefaults source,
        [Frozen] Mock<IMediator> mediator,
        ConfigurationController sut
    )
    {
        // Arrange
        mediator.Setup(value => value.Send(It.IsAny<GetConfigurationDefaults.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(source));

        // Act
        var result = await sut.Defaults(CancellationToken.None);

        // Assert
        result.Value.Should().BeSameAs(source);
    }

    [Theory, AutoMoqData]
    internal async Task Should_DiscoverWithUnsavedConnectionDetails(
        ServiceConnection connection,
        ServicePathSuggestions source,
        [Frozen] Mock<IMediator> mediator,
        ConfigurationController sut
    )
    {
        // Arrange
        var service = MediaService.Transmission;
        mediator.Setup(value => value.Send(
                query: It.Is<DiscoverServicePaths.Query>(query => query.Service == service && query.Connection == connection),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).Returns(ValueTask.FromResult(source));

        // Act
        var result = await sut.Paths(service, connection, CancellationToken.None);

        // Assert
        result.Value.Should().BeSameAs(source);
    }

    [Theory, AutoMoqData]
    internal async Task Should_HideUpstreamCredentialsOnFailure(
        ServiceConnection connection,
        [Frozen] Mock<IMediator> mediator,
        ConfigurationController sut
    )
    {
        // Arrange
        mediator.Setup(value => value.Send(It.IsAny<DiscoverServicePaths.Query>(), It.IsAny<CancellationToken>()))
            .Throws(new HttpRequestException(connection.ApiKey));
        var expectedStatus = 502;

        // Act
        var result = await sut.Paths(MediaService.Radarr, connection, CancellationToken.None);

        // Assert
        var problem = result.Result.Should().BeOfType<ObjectResult>().Which;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().NotContain(connection.ApiKey);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAnUnknownService(ServiceConnection connection, ConfigurationController sut)
    {
        // Arrange
        var service = (MediaService)int.MaxValue;

        // Act
        var result = await sut.Paths(service, connection, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
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
            fixture.Customize<ConfigurationController>(composer => composer.OmitAutoProperties());
            return fixture;
        }
    );
}
