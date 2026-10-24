namespace Jellyfin.Plugin.TransmissionStreaming.Tests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using Jellyfin.Plugin.TransmissionStreaming.Streaming;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Controller.Session;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using HostBootstrapper = Jellyfin.Plugin.TransmissionStreaming.Bootstrapper;

public sealed class BootstrapperTests
{
    [Theory, AutoMoqData]
    internal void Should_BeDiscoverableByTheJellyfinHost(HostBootstrapper sut)
    {
        // Arrange
        var contract = typeof(IPluginServiceRegistrator);

        // Act
        var type = sut.GetType();

        // Assert
        type.IsPublic.Should().BeTrue();
        type.Should().BeAssignableTo(contract);
        type.GetConstructor(Type.EmptyTypes).Should().NotBeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_ComposeThePluginEndpointsAndHostedWorker(Mock<IServerApplicationHost> host,
        Mock<ILibraryManager> library, Mock<IUserManager> users, Mock<IUserDataManager> userData, Mock<ISessionManager> sessions,
        Mock<IStreamingOptionsProvider> options, Mock<IProjectionStore> store, HostBootstrapper sut)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        sut.RegisterServices(services, host.Object);
        services.AddSingleton(library.Object).AddSingleton(users.Object).AddSingleton(userData.Object)
            .AddSingleton(sessions.Object).AddSingleton(options.Object).AddSingleton(store.Object);

        // Act
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var mediator = provider.GetRequiredService<IMediator>();
        var endpoint = ActivatorUtilities.CreateInstance<StreamController>(provider);
        var workers = provider.GetServices<IHostedService>().ToArray();

        // Assert
        mediator.Should().NotBeNull();
        endpoint.Should().NotBeNull();
        workers.Should().ContainSingle();
        provider.GetRequiredService<IProjectionLibrary>().Should().BeOfType<LibraryProjection>();
        provider.GetServices<IResolverIgnoreRule>().Should().ContainSingle().Which.Should().BeOfType<ImportedFileIgnoreRule>();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
