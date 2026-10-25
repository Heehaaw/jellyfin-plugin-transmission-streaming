namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;

using AutoFixture;
using global::TransmissionStreaming.TestSupport.Testing;
using Xunit;

/// <summary>Resolves generated application subjects against the single assembly-owned setup.</summary>
public abstract class ApplicationDataAttribute(Func<IFixture> createFixture) : AutoDataAttribute(createFixture)
{
    /// <inheritdoc />
    protected override async ValueTask CustomizeAsync(IFixture fixture)
    {
        var application = await TestContext.Current.GetFixture<JellyfinApplication>()
                          ?? throw new InvalidOperationException("Register the Jellyfin application assembly fixture.");
        fixture.Inject(application);
        fixture.Inject(application.Downloads);
    }
}
