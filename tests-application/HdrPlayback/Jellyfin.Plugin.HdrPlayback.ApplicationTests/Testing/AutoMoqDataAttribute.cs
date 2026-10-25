namespace Jellyfin.Plugin.HdrPlayback.ApplicationTests.Testing;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Kernel;
using Hosting;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

internal sealed class AutoMoqDataAttribute : DataAttribute
{
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var server = await TestContext.Current.GetFixture<HdrJellyfinFixture>()
                     ?? throw new InvalidOperationException("Register the Jellyfin assembly fixture.");
        var fixture = new Fixture
        {
            RepeatCount = 1
        };
        fixture.Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true
        });
        fixture.Inject(server);
        var context = new SpecimenContext(fixture);
        return [new TheoryDataRow(testMethod.GetParameters().Select(context.Resolve).ToArray())];
    }

    public override bool SupportsDiscoveryEnumeration()
    {
        return false;
    }
}
