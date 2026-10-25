namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class InferMediaRootTests
{
    [Theory, AutoMoqData]
    internal async Task Should_InferTheCommonParentOfLibraryFolders(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        var root = Path.GetDirectoryName(source.ProjectionRoot)!;
        source = source with
        {
            LibraryPaths = [Path.Combine(root, "movies"), Path.Combine(root, "shows")]
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Should().Be(root);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ExcludeTheOwnedProjectionAndItsDescendants(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        var media = Path.Combine(Path.GetDirectoryName(source.ProjectionRoot)!, "media");
        source = source with
        {
            LibraryPaths = [source.ProjectionRoot, Path.Combine(source.ProjectionRoot, "movies"), Path.Combine(media, "movies")]
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Should().Be(media);
    }

    [Theory, AutoMoqData]
    internal async Task Should_IgnoreInvalidAndRelativeFolders(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        var root = Path.GetDirectoryName(source.ProjectionRoot)!;
        var relative = source.LibraryPaths[0];
        source = source with
        {
            LibraryPaths = [relative, " ", Path.Combine(root, "invalid\0folder"), Path.Combine(root, "movies")]
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Should().Be(root);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseFoldersWhoseOnlyCommonParentIsTheFilesystemRoot(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        var root = Path.GetDirectoryName(source.ProjectionRoot)!;
        var otherRoot = Path.Combine(Path.GetPathRoot(root)!, Path.GetFileName(root) + "-other");
        source = source with
        {
            LibraryPaths = [Path.Combine(root, "movies"), Path.Combine(otherRoot, "shows")]
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveAnEmptyLibraryUndecided(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        source = source with
        {
            LibraryPaths = []
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RefuseAnUnqualifiedProjectionRoot(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        source = source with
        {
            ProjectionRoot = Path.GetFileName(source.ProjectionRoot)
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_HonorCancellationBeforeInference(InferMediaRoot.Query source, InferMediaRoot.Handler sut)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var action = async () => await sut.Handle(source, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
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
            fixture.Customize<InferMediaRoot.Query>(composer => composer
                .With(value => value.ProjectionRoot, () => Path.Combine(Path.GetTempPath(), fixture.Create<string>(), "projection"))
            );
            return fixture;
        }
    );
}
