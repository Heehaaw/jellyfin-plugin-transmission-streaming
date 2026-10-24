namespace TransmissionStreaming.Infrastructure.Tests;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

internal sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
{
    var fixture = new Fixture { RepeatCount = 1 };
    fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
    return fixture;
});
