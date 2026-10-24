namespace TransmissionStreaming.Logic.Tests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using TransmissionStreaming.Logic.Configuration;
using Xunit;

public sealed class MediaRootInferenceTests
{
    [Theory, AutoMoqData]
    internal void Should_InferTheCommonParentOfLibraryFolders(string name, MediaRootInference sut)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), name);
        var projection = Path.Combine(root, "plugin", "library");
        string[] folders = [Path.Combine(root, "movies"), Path.Combine(root, "shows")];

        // Act
        var result = sut.Infer(folders, projection);

        // Assert
        result.Should().Be(root);
    }

    [Theory, AutoMoqData]
    internal void Should_ExcludeTheOwnedProjectionAndItsDescendants(string name, MediaRootInference sut)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), name);
        var media = Path.Combine(root, "media");
        var projection = Path.Combine(root, "plugin", "library");
        string[] folders = [projection, Path.Combine(projection, "movies"), Path.Combine(media, "movies")];

        // Act
        var result = sut.Infer(folders, projection);

        // Assert
        result.Should().Be(media);
    }

    [Theory, AutoMoqData]
    internal void Should_IgnoreInvalidAndRelativeFolders(string name, MediaRootInference sut)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), name);
        string[] folders = [name, " ", root + Path.DirectorySeparatorChar + "invalid\0folder", Path.Combine(root, "movies")];

        // Act
        var result = sut.Infer(folders, Path.Combine(root, "projection"));

        // Assert
        result.Should().Be(root);
    }

    [Theory, AutoMoqData]
    internal void Should_RefuseFoldersWhoseOnlyCommonParentIsTheFilesystemRoot(string name, MediaRootInference sut)
    {
        // Arrange
        var filesystemRoot = Path.GetPathRoot(Path.GetTempPath())!;
        string[] folders = [Path.Combine(filesystemRoot, name, "movies"), Path.Combine(filesystemRoot, name + "-other", "shows")];

        // Act
        var result = sut.Infer(folders, Path.Combine(filesystemRoot, name, "projection"));

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveAnEmptyLibraryUndecided(string name, MediaRootInference sut)
    {
        // Arrange
        var projection = Path.Combine(Path.GetTempPath(), name);

        // Act
        var result = sut.Infer([], projection);

        // Assert
        result.Should().BeNull();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
