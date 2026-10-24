namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Filesystem;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class MapServicePathTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ApplyTheSelectedServiceMapping(MapServicePath.Query query, List<ServicePathMapping> mappings, string translated,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaPathMapper> mapper, MapServicePath.Handler sut)
    {
        // Arrange
        var mapping = mappings[0];
        query = query with { Path = mapping.RemoteRoot + "/video.mkv" };
        mediator.Setup(value => value.Send(It.Is<GetServicePathMappings.Query>(request => request.Service == query.Service), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>(mappings));
        mapper.Setup(value => value.Map(query.Path, mapping.RemoteRoot, mapping.LocalRoot)).Returns(translated);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(translated);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreferTheMostSpecificRoot(MapServicePath.Query query, List<ServicePathMapping> mappings, string translated,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaPathMapper> mapper, MapServicePath.Handler sut)
    {
        // Arrange
        var nested = mappings[1];
        nested.RemoteRoot = mappings[0].RemoteRoot + "/nested";
        query = query with { Path = nested.RemoteRoot + "/video.mkv" };
        mediator.Setup(value => value.Send(It.IsAny<GetServicePathMappings.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>(mappings));
        mapper.Setup(value => value.Map(query.Path, nested.RemoteRoot, nested.LocalRoot)).Returns(translated);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(translated);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseConflictingTranslations(MapServicePath.Query query, List<ServicePathMapping> mappings,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaPathMapper> mapper, MapServicePath.Handler sut)
    {
        // Arrange
        mappings[1].RemoteRoot = mappings[0].RemoteRoot;
        query = query with { Path = mappings[0].RemoteRoot + "/video.mkv" };
        mediator.Setup(value => value.Send(It.IsAny<GetServicePathMappings.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>(mappings));

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        mapper.Verify(value => value.Map(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefusePathsOutsideEveryRoot(MapServicePath.Query query, List<ServicePathMapping> mappings,
        [Frozen] Mock<IMediator> mediator, [Frozen] Mock<IMediaPathMapper> mapper, MapServicePath.Handler sut)
    {
        // Arrange
        query = query with { Path = "/unmapped/video.mkv" };
        mediator.Setup(value => value.Send(It.IsAny<GetServicePathMappings.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>(mappings));

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        mapper.Verify(value => value.Map(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 2 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<ServicePathMapping>(composer => composer.With(value => value.RemoteRoot, () => "/remote/" + fixture.Create<string>()));
        return fixture;
    });
}
