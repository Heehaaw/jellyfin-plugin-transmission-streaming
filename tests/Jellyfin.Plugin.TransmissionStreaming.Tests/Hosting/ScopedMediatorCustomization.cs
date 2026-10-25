namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Hosting;

using AutoFixture;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>Routes host entry scopes to the same frozen mediator controlled by each test.</summary>
internal sealed class ScopedMediatorCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var mediator = fixture.Freeze<Mock<IMediator>>();
        var services = fixture.Freeze<Mock<IServiceProvider>>();
        services.Setup(value => value.GetService(typeof(IMediator))).Returns(mediator.Object);

        var scope = fixture.Freeze<Mock<IServiceScope>>();
        scope.SetupGet(value => value.ServiceProvider).Returns(services.Object);

        var scopes = fixture.Freeze<Mock<IServiceScopeFactory>>();
        scopes.Setup(value => value.CreateScope()).Returns(scope.Object);
    }
}
