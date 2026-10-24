namespace TransmissionStreaming.Core.Tests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

internal sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
{
    var fixture = new Fixture();
    fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
    return fixture;
});
