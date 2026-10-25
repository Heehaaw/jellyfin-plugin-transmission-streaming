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

public sealed class ValidateLocalMediaPathTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ConfineAStoredImportToItsServiceRoot(
        ValidateLocalMediaPath.Query query,
        ServicePathMapping mapping,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaPathMapper> mapper,
        ValidateLocalMediaPath.Handler sut
    )
    {
        // Arrange
        query = query with
        {
            Path = mapping.LocalRoot + "/video.mkv"
        };
        mediator.Setup(value => value.Send(It.Is<GetServicePathMappings.Query>(request => request.Service == query.Service), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>([mapping]));
        mapper.Setup(value => value.Map(query.Path, mapping.LocalRoot, mapping.LocalRoot)).Returns(query.Path);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(query.Path);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RespectPhysicalConfinementRefusals(
        ValidateLocalMediaPath.Query query,
        ServicePathMapping mapping,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaPathMapper> mapper,
        ValidateLocalMediaPath.Handler sut
    )
    {
        // Arrange
        query = query with
        {
            Path = mapping.LocalRoot + "/../other.mkv"
        };
        mediator.Setup(value => value.Send(It.IsAny<GetServicePathMappings.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>([mapping]));
        mapper.Setup(value => value.Map(query.Path, mapping.LocalRoot, mapping.LocalRoot)).Returns((string?)null);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseAnImportOutsideItsCurrentRoots(
        ValidateLocalMediaPath.Query query,
        ServicePathMapping mapping,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IMediaPathMapper> mapper,
        ValidateLocalMediaPath.Handler sut
    )
    {
        // Arrange
        query = query with
        {
            Path = "/unmapped/video.mkv"
        };
        mediator.Setup(value => value.Send(It.IsAny<GetServicePathMappings.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<ServicePathMapping>>([mapping]));

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        mapper.Verify(value => value.Map(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            fixture.Customize<ServicePathMapping>(composer => composer.With(value => value.LocalRoot, "/local/" + fixture.Create<string>()));
            return fixture;
        }
    );
}
