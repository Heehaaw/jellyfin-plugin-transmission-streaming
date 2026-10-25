namespace TransmissionStreaming.IntegrationTests.Infrastructure;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using Filesystem;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Discovery;
using global::TransmissionStreaming.Core.Filesystem;
using global::TransmissionStreaming.Core.Projection;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Core.Transmission;
using global::TransmissionStreaming.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public sealed class BootstrapperTests
{
    [Theory, AutoMoqData]
    internal void Should_ComposeEveryInfrastructureFeature(
        StreamingOptions source,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ServiceCollection services
    )
    {
        // Arrange
        using var root = directory;
        options.SetupGet(value => value.CurrentValue).Returns(
            source with
            {
                StateDirectory = root.Path
            }
        );
        services.AddSingleton(options.Object);
        services.AddTransmissionStreamingInfrastructure();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }
        );
        Type[] contracts =
        [
            typeof(IArrClient), typeof(ITransmissionClient), typeof(IServicePathDiscovery), typeof(IMediaPathMapper), typeof(IMediaFiles),
            typeof(IProjectionFiles),
            typeof(IProjectionStore), typeof(IStreamActivity), typeof(IDiscoveryStatus)
        ];

        // Act
        var resolved = contracts.Select(provider.GetRequiredService).ToArray();

        // Assert
        resolved.Should().HaveCount(contracts.Length).And.NotContainNulls();
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
        provider.GetRequiredService<IStreamActivity>().Should().BeSameAs(provider.GetRequiredService<IStreamActivity>());
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveAHostClockOverride(Mock<TimeProvider> clock, ServiceCollection services)
    {
        // Arrange
        services.AddSingleton(clock.Object);

        // Act
        services.AddTransmissionStreamingInfrastructure();
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(clock.Object);
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
            fixture.Register(() => new ServiceCollection());
            fixture.Freeze<Mock<IOptionsMonitor<StreamingOptions>>>();
            fixture.Freeze<Mock<TimeProvider>>();
            return fixture;
        }
    );
}
