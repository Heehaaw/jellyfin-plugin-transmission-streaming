namespace TransmissionStreaming.TestSupport.Testing;

using AutoFixture;
using AutoFixture.Kernel;
using System.Reflection;
using Torrents;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

/// <summary>Creates generated test data at execution time so SUTs can borrow the assembly-owned containers.</summary>
public abstract class AutoDataAttribute(Func<IFixture> createFixture) : DataAttribute
{
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var fixture = createFixture();
        await CustomizeAsync(fixture);
        var context = new SpecimenContext(fixture);
        var arguments = testMethod.GetParameters().Select(context.Resolve).ToArray();
        return [new TheoryDataRow(arguments)];
    }

    /// <summary>Injects the assembly-owned external services before resolving generated test inputs.</summary>
    protected virtual async ValueTask CustomizeAsync(IFixture fixture)
    {
        var containers = await TestContext.Current.GetFixture<DownloadServices>()
                         ?? throw new InvalidOperationException("Register DownloadServices as an xUnit assembly fixture.");
        fixture.Inject(containers);
    }

    public override bool SupportsDiscoveryEnumeration()
    {
        // Discovery has no live assembly fixture and must not start external processes.
        return false;
    }
}
