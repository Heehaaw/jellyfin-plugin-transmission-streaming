namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class GetServicePathMappingsTests
{
    [Theory, AutoMoqData]
    internal async Task Should_SelectOnlyTheRequestedService(StreamingOptions source, GetServicePathMappings.Query query,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, GetServicePathMappings.Handler sut)
    {
        // Arrange
        source.PathMappings[0].Service = MediaService.Transmission;
        source.PathMappings[1].Service = MediaService.Radarr;
        query = query with { Service = source.PathMappings[0].Service };
        options.SetupGet(value => value.CurrentValue).Returns(source);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Equal(source.PathMappings[0]);
    }

    [Theory, AutoMoqData]
    internal async Task Should_UseTheSharedFallbackWhenNoServiceMappingsExist(StreamingOptions source, GetServicePathMappings.Query query,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, GetServicePathMappings.Handler sut)
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(source with { PathMappings = [] });

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        var mapping = result.Should().ContainSingle().Which;
        mapping.Service.Should().Be(query.Service);
        mapping.RemoteRoot.Should().Be(source.RemoteMediaRoot);
        mapping.LocalRoot.Should().Be(source.LocalMediaRoot);
    }

    [Theory, AutoMoqData]
    internal async Task Should_NotReplaceIncompleteExplicitRowsWithTheFallback(StreamingOptions source, GetServicePathMappings.Query query,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options, GetServicePathMappings.Handler sut)
    {
        // Arrange
        var mapping = source.PathMappings[0];
        mapping.Service = query.Service;
        mapping.LocalRoot = "";
        options.SetupGet(value => value.CurrentValue).Returns(source with { PathMappings = [mapping] });

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle().Which.Should().BeSameAs(mapping);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 2 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
