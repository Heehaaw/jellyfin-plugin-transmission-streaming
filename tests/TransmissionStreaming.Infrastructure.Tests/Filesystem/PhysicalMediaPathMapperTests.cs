namespace TransmissionStreaming.Infrastructure.Tests.Filesystem;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Infrastructure.Filesystem;
using Xunit;

public sealed class PhysicalMediaPathMapperTests
{
    [Theory, AutoMoqData]
    internal void Should_ApplyTheSelectedPhysicalRootPair(ServicePathMapping source, string fileName, PhysicalMediaPathMapper sut)
    {
        // Arrange
        var remote = source.RemoteRoot + "/" + fileName;
        var expected = Path.Combine(source.LocalRoot, fileName);

        // Act
        var result = sut.Map(remote, source.RemoteRoot, source.LocalRoot);

        // Assert
        result.Should().Be(expected);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<ServicePathMapping>(composer => composer
            .With(value => value.RemoteRoot, "/remote/" + fixture.Create<string>())
            .With(value => value.LocalRoot, Path.Combine(Path.GetTempPath(), fixture.Create<string>())));
        return fixture;
    });
}
