namespace Jellyfin.Plugin.TransmissionStreaming.IntegrationTests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Logic;
using global::TransmissionStreaming.Logic.Discovery;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class LogicBootstrapperTests
{
    [Theory, AutoMoqData]
    internal async Task Should_DispatchHandlersThroughTheLogicCompositionRoot(SelectVideoFiles.Query source, ServiceCollection services)
    {
        // Arrange
        services.Clear();
        services.AddTransmissionStreamingLogic();
        foreach (var file in source.Files)
        {
            file.Name += ".mkv";
        }

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            }
        );
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        using var secondScope = provider.CreateScope();
        var mediator = scopedServices.GetRequiredService<IMediator>();
        var expectedIndexes = Enumerable.Range(0, source.Files.Count);

        // Act
        var result = await mediator.Send(source);
        var repeatedMediator = scopedServices.GetRequiredService<IMediator>();
        var otherMediator = secondScope.ServiceProvider.GetRequiredService<IMediator>();
        var handler = scopedServices.GetRequiredService<IQueryHandler<SelectVideoFiles.Query, SelectVideoFiles.Result>>();
        var repeatedHandler = scopedServices.GetRequiredService<IQueryHandler<SelectVideoFiles.Query, SelectVideoFiles.Result>>();
        var otherHandler = secondScope.ServiceProvider.GetRequiredService<IQueryHandler<SelectVideoFiles.Query, SelectVideoFiles.Result>>();
        var logicAssembly = typeof(SelectVideoFiles).Assembly.FullName;
        var mediatorAssembly = mediator.GetType().Assembly.FullName;

        // Assert
        result.PlayableIndexes.Should().Equal(expectedIndexes);
        mediatorAssembly.Should().Be(logicAssembly);
        mediator.Should().BeSameAs(repeatedMediator).And.NotBeSameAs(otherMediator);
        handler.Should().BeSameAs(repeatedHandler).And.NotBeSameAs(otherHandler);
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
            fixture.Customize<ServiceCollection>(composer => composer.OmitAutoProperties());
            return fixture;
        }
    );
}
