namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Options;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class GetConfigurationDefaultsTests
{
    [Theory, AutoMoqData]
    internal async Task Should_RequestInferenceForMissingLocalPaths(
        StreamingOptions source,
        GetConfigurationDefaults.Query query,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaLibraryLocations> libraries,
        [Frozen] Mock<IMediator> mediator,
        GetConfigurationDefaults.Handler sut
    )
    {
        // Arrange
        var media = Path.Combine(source.StateDirectory, "media");
        string[] folders = [Path.Combine(media, "movies"), Path.Combine(media, "shows")];
        var projection = Path.Combine(source.StateDirectory, "library");
        options.SetupGet(value => value.CurrentValue).Returns(
            source with
            {
                ProjectionRoot = "",
                LocalMediaRoot = ""
            }
        );
        libraries.Setup(value => value.GetPaths()).Returns(folders);
        mediator.Setup(value => value.Send(
                    query: It.Is<IQuery<string?>>(request => request is InferMediaRoot.Query
                                                             && ((InferMediaRoot.Query)request).ProjectionRoot == projection &&
                                                             ((InferMediaRoot.Query)request).LibraryPaths == folders
                    ),
                    cancellationToken: CancellationToken.None
                )
            )
            .Returns(ValueTask.FromResult<string?>(media));

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.ProjectionRoot.Should().Be(projection);
        result.LocalMediaRoot.Should().Be(media);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveExplicitPathsWithoutInspectingLibraries(
        StreamingOptions source,
        GetConfigurationDefaults.Query query,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaLibraryLocations> libraries,
        [Frozen] Mock<IMediator> mediator,
        GetConfigurationDefaults.Handler sut
    )
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(source);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.ProjectionRoot.Should().Be(source.ProjectionRoot);
        result.LocalMediaRoot.Should().Be(source.LocalMediaRoot);
        libraries.Verify(value => value.GetPaths(), Times.Never);
        mediator.Verify(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepTheOwnedFolderWhenInferenceIsInconclusive(
        StreamingOptions source,
        GetConfigurationDefaults.Query query,
        [Frozen] Mock<IOptionsMonitor<StreamingOptions>> options,
        [Frozen] Mock<IMediaLibraryLocations> libraries,
        [Frozen] Mock<IMediator> mediator,
        GetConfigurationDefaults.Handler sut
    )
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(
            source with
            {
                ProjectionRoot = " ",
                LocalMediaRoot = ""
            }
        );
        libraries.Setup(value => value.GetPaths()).Returns([]);
        mediator.Setup(value => value.Send(It.IsAny<IQuery<string?>>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult<string?>(null));

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.ProjectionRoot.Should().Be(Path.Combine(source.StateDirectory, "library"));
        result.LocalMediaRoot.Should().BeNull();
        mediator.Verify(value => value.Send(It.Is<IQuery<string?>>(request => request is InferMediaRoot.Query), CancellationToken.None), Times.Once);
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
            fixture.Customize<StreamingOptions>(composer => composer.With(value => value.StateDirectory, () => Path.Combine(Path.GetTempPath(), fixture.Create<string>())));
            return fixture;
        }
    );
}
