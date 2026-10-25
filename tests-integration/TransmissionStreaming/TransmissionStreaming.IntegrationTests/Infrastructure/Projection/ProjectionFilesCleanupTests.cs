namespace TransmissionStreaming.IntegrationTests.Infrastructure.Projection;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using Filesystem;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Filesystem;
using global::TransmissionStreaming.Infrastructure.Filesystem;
using global::TransmissionStreaming.Infrastructure.Projection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ProjectionEntry = global::TransmissionStreaming.Core.Projection.Projection;

public sealed class ProjectionFilesCleanupTests
{
    [Theory, AutoMoqData]
    internal void Should_RemoveOnlyThisEntriesGeneratedFilesAndStagingFiles(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path,
                JellyfinPublicUrl = "http://jellyfin.invalid"
            }
        );
        sut.EnsureReady();
        source.Identity.Kind = MediaKind.Movie;
        source.StreamPath = Path.Combine(root.Path, "movies", source.Id + ".strm");
        sut.WriteFiles(source);
        var metadata = Path.ChangeExtension(source.StreamPath, ".nfo");
        string[] generated = [source.StreamPath, metadata, source.StreamPath + ".new", metadata + ".new"];
        File.WriteAllText(source.StreamPath + ".new", source.Id);
        File.WriteAllText(metadata + ".new", source.Id);
        var media = Path.ChangeExtension(source.StreamPath, ".mkv");
        var sharedShow = Path.Combine(root.Path, "shows", "tvshow.nfo");
        File.WriteAllText(media, source.SourceName);
        File.WriteAllText(sharedShow, source.SourceName);

        // Act
        sut.RemoveFiles(source);
        sut.RemoveFiles(source);

        // Assert
        generated.Should().OnlyContain(path => !File.Exists(path));
        File.ReadAllText(media).Should().Be(source.SourceName);
        File.ReadAllText(sharedShow).Should().Be(source.SourceName);
        Directory.Exists(Path.Combine(root.Path, "movies")).Should().BeTrue();
        File.Exists(Path.Combine(root.Path, ".transmission-streaming")).Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveAnAlreadyMissingRootAbsent(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        var missing = Path.Combine(root.Path, source.Id);
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = missing
            }
        );
        source.StreamPath = Path.Combine(missing, source.Id + ".strm");

        // Act
        sut.RemoveFiles(source);

        // Assert
        Directory.Exists(missing).Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_RequireTheRootsOwnershipMarkerBeforeDeletingFiles(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path
            }
        );
        source.StreamPath = Path.Combine(root.Path, source.Id + ".strm");
        File.WriteAllText(source.StreamPath, source.SourceName);

        // Act
        var action = () => sut.RemoveFiles(source);

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(source.StreamPath).Should().Be(source.SourceName);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectEscapingAndTraversalPaths(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        TemporaryDirectory outside,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        using var external = outside;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path
            }
        );
        sut.EnsureReady();
        var outsideFile = Path.Combine(external.Path, source.Id + ".strm");
        var insideFile = Path.Combine(root.Path, source.Id + ".strm");
        File.WriteAllText(outsideFile, source.SourceName);
        File.WriteAllText(insideFile, source.SourceName);
        string[] invalidPaths = [outsideFile, Path.Combine(root.Path, "movies", "..", source.Id + ".strm")];

        foreach (var invalid in invalidPaths)
        {
            source.StreamPath = invalid;

            // Act
            var action = () => sut.RemoveFiles(source);

            // Assert
            action.Should().Throw<InvalidOperationException>();
        }

        File.ReadAllText(outsideFile).Should().Be(source.SourceName);
        File.ReadAllText(insideFile).Should().Be(source.SourceName);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectDirectorySymlinksBelowTheOwnedRoot(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        TemporaryDirectory outside,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        using var external = outside;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path
            }
        );
        sut.EnsureReady();
        var original = Path.Combine(external.Path, source.Id + ".strm");
        File.WriteAllText(original, source.SourceName);
        var link = Path.Combine(root.Path, "movies", source.Id);
        Directory.CreateSymbolicLink(link, external.Path);
        source.StreamPath = Path.Combine(link, Path.GetFileName(original));

        // Act
        var action = () => sut.RemoveFiles(source);

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(original).Should().Be(source.SourceName);
    }

    [Theory, AutoMoqData]
    internal void Should_ValidateEveryTargetBeforeDeletingTheFirstFile(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        TemporaryDirectory outside,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        using var external = outside;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path
            }
        );
        sut.EnsureReady();
        source.StreamPath = Path.Combine(root.Path, "movies", source.Id + ".strm");
        var metadata = Path.ChangeExtension(source.StreamPath, ".nfo");
        var payload = Path.Combine(external.Path, source.Id + ".mkv");
        File.WriteAllText(source.StreamPath, source.SourceName);
        File.WriteAllText(metadata, source.SourceName);
        File.WriteAllText(payload, source.SourceName);
        File.CreateSymbolicLink(metadata + ".new", payload);

        // Act
        var action = () => sut.RemoveFiles(source);

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(source.StreamPath).Should().Be(source.SourceName);
        File.ReadAllText(metadata).Should().Be(source.SourceName);
        File.ReadAllText(payload).Should().Be(source.SourceName);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectASymbolicLinkAsTheProjectionRoot(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        TemporaryDirectory outside,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        using var external = outside;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = external.Path
            }
        );
        sut.EnsureReady();
        var original = Path.Combine(external.Path, source.Id + ".strm");
        File.WriteAllText(original, source.SourceName);
        var alias = Path.Combine(root.Path, source.Id);
        Directory.CreateSymbolicLink(alias, external.Path);
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = alias
            }
        );
        source.StreamPath = Path.Combine(alias, Path.GetFileName(original));

        // Act
        var action = () => sut.RemoveFiles(source);

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(original).Should().Be(source.SourceName);
    }

    [Theory, AutoMoqData]
    internal void Should_NeverTreatAnActualMediaPayloadAsAStreamFile(
        ProjectionEntry source,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path
            }
        );
        sut.EnsureReady();
        source.StreamPath = Path.Combine(root.Path, "movies", source.Id + ".mkv");
        File.WriteAllText(source.StreamPath, source.SourceName);

        // Act
        var action = () => sut.RemoveFiles(source);

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(source.StreamPath).Should().Be(source.SourceName);
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
            fixture.Register<IMediaPathMapper>(() => fixture.Create<PhysicalMediaPathMapper>());
            fixture.Freeze<Mock<IOptionsMonitor<StreamingOptions>>>();
            return fixture;
        }
    );
}
