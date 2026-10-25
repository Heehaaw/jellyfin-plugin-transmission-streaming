namespace HdrPlayback.IntegrationTests.Testing;

using AutoFixture;
using AutoFixture.Kernel;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

internal abstract class AutoDataAttribute(Func<IFixture> createFixture) : DataAttribute
{
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var context = new SpecimenContext(createFixture());
        var arguments = testMethod.GetParameters().Select(context.Resolve).ToArray();
        return ValueTask.FromResult<IReadOnlyCollection<ITheoryDataRow>>([new TheoryDataRow(arguments)]);
    }

    public override bool SupportsDiscoveryEnumeration()
    {
        return false;
    }
}
