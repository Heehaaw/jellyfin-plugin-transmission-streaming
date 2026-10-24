namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Moq;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class GetConfigurationDefaultsTests
{
    [Theory, AutoMoqData]
    internal async Task Should_DeriveMissingLocalPathsFromTheHost(StreamingOptions source, GetConfigurationDefaults.Query query,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IMediaLibraryLocations> libraries, GetConfigurationDefaults.Handler sut)
    {
        // Arrange
        var media = Path.Combine(source.StateDirectory, "media");
        options.Setup(value => value.Get()).Returns(source with { ProjectionRoot = "", LocalMediaRoot = "" });
        libraries.Setup(value => value.GetPaths()).Returns([Path.Combine(media, "movies"), Path.Combine(media, "shows")]);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.ProjectionRoot.Should().Be(Path.Combine(source.StateDirectory, "library"));
        result.LocalMediaRoot.Should().Be(media);
    }

    [Theory, AutoMoqData]
    internal async Task Should_PreserveExplicitPathsWithoutInspectingLibraries(StreamingOptions source, GetConfigurationDefaults.Query query,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IMediaLibraryLocations> libraries, GetConfigurationDefaults.Handler sut)
    {
        // Arrange
        options.Setup(value => value.Get()).Returns(source);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.ProjectionRoot.Should().Be(source.ProjectionRoot);
        result.LocalMediaRoot.Should().Be(source.LocalMediaRoot);
        libraries.Verify(value => value.GetPaths(), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_SuggestOnlyTheOwnedFolderWhenLibrariesAreInconclusive(StreamingOptions source, GetConfigurationDefaults.Query query,
        [Frozen] Mock<IStreamingOptionsProvider> options, [Frozen] Mock<IMediaLibraryLocations> libraries, GetConfigurationDefaults.Handler sut)
    {
        // Arrange
        options.Setup(value => value.Get()).Returns(source with { ProjectionRoot = " ", LocalMediaRoot = "" });
        libraries.Setup(value => value.GetPaths()).Returns([]);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.ProjectionRoot.Should().Be(Path.Combine(source.StateDirectory, "library"));
        result.LocalMediaRoot.Should().BeNull();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Customize<StreamingOptions>(composer => composer.With(value => value.StateDirectory, () => Path.Combine(Path.GetTempPath(), fixture.Create<string>())));
        fixture.Register<IMediaRootInference>(() => fixture.Create<MediaRootInference>());
        return fixture;
    });
}
