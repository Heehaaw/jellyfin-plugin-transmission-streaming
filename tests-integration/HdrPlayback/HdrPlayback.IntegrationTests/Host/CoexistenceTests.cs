namespace HdrPlayback.IntegrationTests.Host;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using HdrPlayback.Core.Playback;
using HdrPlayback.Logic.Playback;
using Mediator;
using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testing;
using TransmissionStreaming.Logic.Discovery;
using Xunit;
using HdrBootstrapper = Jellyfin.Plugin.HdrPlayback.Bootstrapper;
using TransmissionBootstrapper = Jellyfin.Plugin.TransmissionStreaming.Bootstrapper;

public sealed class CoexistenceTests
{
    [Theory, AutoMoqData]
    internal async Task Should_DispatchBothPluginsWhenHdrLoadsLast(
        HdrPlaybackContext source,
        SelectVideoFiles.Query selection,
        ServiceCollection services,
        Mock<IServerApplicationHost> host,
        Mock<ITranscodeManager> native,
        TransmissionBootstrapper transmission,
        HdrBootstrapper sut)
    {
        // Arrange
        services.Clear();
        services.AddLogging();
        services.AddSingleton(native.Object);
        transmission.RegisterServices(services, host.Object);
        sut.RegisterServices(services, host.Object);

        // Act
        var results = await DispatchAsync(services, source, selection);

        // Assert
        results.Hdr.Enabled.Should().BeTrue();
        results.TransmissionAssembly.FullName.Should().Be(typeof(SelectVideoFiles).Assembly.FullName);
    }

    [Theory, AutoMoqData]
    internal async Task Should_DispatchBothPluginsWhenHdrLoadsFirst(
        HdrPlaybackContext source,
        SelectVideoFiles.Query selection,
        ServiceCollection services,
        Mock<IServerApplicationHost> host,
        Mock<ITranscodeManager> native,
        TransmissionBootstrapper transmission,
        HdrBootstrapper sut)
    {
        // Arrange
        services.Clear();
        services.AddLogging();
        services.AddSingleton(native.Object);
        sut.RegisterServices(services, host.Object);
        transmission.RegisterServices(services, host.Object);

        // Act
        var results = await DispatchAsync(services, source, selection);

        // Assert
        results.Hdr.Enabled.Should().BeTrue();
        results.TransmissionAssembly.FullName.Should().Be(typeof(SelectVideoFiles).Assembly.FullName);
    }

    private static async Task<DispatchResult> DispatchAsync(ServiceCollection services, HdrPlaybackContext context, SelectVideoFiles.Query selection)
    {
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();
        var hdr = scope.ServiceProvider.GetRequiredKeyedService<ISender>(typeof(SelectHdrMode));
        var transmission = scope.ServiceProvider.GetRequiredService<IMediator>();
        context = context with
        {
            Requested = true,
            SourceIsSdr = true,
            ClientSupportsHdr = true,
            CanTranscode = true,
            PipelineSupported = true
        };
        _ = await transmission.Send(selection);
        return new DispatchResult(await hdr.Send(new SelectHdrMode.Query(context), CancellationToken.None), transmission.GetType().Assembly);
    }

    private sealed record DispatchResult(HdrPlaybackDecision Hdr, System.Reflection.Assembly TransmissionAssembly);

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture
        {
            RepeatCount = 1
        };
        fixture.Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true
        });
        fixture.Customize<ServiceCollection>(composer => composer.OmitAutoProperties());
        return fixture;
    }
    );
}
