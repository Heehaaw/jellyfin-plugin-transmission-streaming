namespace TransmissionStreaming.IntegrationTests.Infrastructure.Projection;

using Filesystem;
using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Infrastructure.Projection;
using Microsoft.Extensions.Options;
using Moq;
using System.Xml.Linq;
using Xunit;
using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using ProjectionEntry = global::TransmissionStreaming.Core.Projection.Projection;

public sealed class ProjectionFilesTests
{
    [Theory, AutoMoqData]
    internal void Should_RefuseAnUnrecognizedOwnershipMarker(
        string markerValue,
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
        var marker = Path.Combine(root.Path, ".transmission-streaming");
        File.WriteAllText(marker, markerValue);

        // Act
        var action = sut.EnsureReady;

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(marker).Should().Be(markerValue);
        Directory.GetFileSystemEntries(root.Path).Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal void Should_RefuseToAdoptExistingContents(
        string name,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        var existing = Path.Combine(root.Path, name);
        File.WriteAllText(existing, name);
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                ProjectionRoot = root.Path
            }
        );

        // Act
        var action = sut.EnsureReady;

        // Assert
        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(existing).Should().Be(name);
        Directory.GetFileSystemEntries(root.Path).Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal void Should_ClaimAndReuseADedicatedEmptyDirectory(
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

        // Act
        sut.EnsureReady();
        sut.EnsureReady();

        // Assert
        Directory.Exists(Path.Combine(root.Path, "movies")).Should().BeTrue();
        Directory.Exists(Path.Combine(root.Path, "shows")).Should().BeTrue();
        Directory.GetFiles(root.Path).Should().ContainSingle();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectTheFilesystemRoot(
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
                ProjectionRoot = Path.GetPathRoot(root.Path)!
            }
        );

        // Act
        var action = sut.EnsureReady;

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_WriteStableMovieLinkAndEscapedMetadata(
        ProjectionEntry entry,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        entry.Identity.Kind = MediaKind.Movie;
        entry.Identity.Title += " <>&";
        entry.StreamPath = Path.Combine(root.Path, entry.Id + ".strm");
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                JellyfinPublicUrl = "https://jellyfin.invalid/base"
            }
        );

        // Act
        sut.WriteFiles(entry);
        var link = new Uri(File.ReadAllText(entry.StreamPath).Trim());
        var metadata = XDocument.Load(Path.ChangeExtension(entry.StreamPath, ".nfo")).Root!;

        // Assert
        link.AbsolutePath.Should().Be($"/base/TransmissionStreaming/streams/{entry.Id}");
        (link.Query == "?key=" + entry.Secret).Should().BeTrue("the capability belongs to this permanent projection");
        metadata.Name.LocalName.Should().Be("movie");
        metadata.Element("title")!.Value.Should().Be(entry.Identity.Title);
        metadata.Elements("uniqueid").Should().Contain(value => value.Attribute("type")!.Value == "tmdb" && value.Value == entry.Identity.TmdbId.ToString());
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(entry.StreamPath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Theory, AutoMoqData]
    internal void Should_WriteEpisodeAndSeriesSidecars(
        ProjectionEntry entry,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        entry.Identity.Kind = MediaKind.Episode;
        entry.StreamPath = Path.Combine(root.Path, entry.Id, "season", entry.Id + ".strm");
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                JellyfinPublicUrl = "https://jellyfin.invalid"
            }
        );

        // Act
        sut.WriteFiles(entry);
        var episode = XDocument.Load(Path.ChangeExtension(entry.StreamPath, ".nfo")).Root!;
        var series = XDocument.Load(Path.Combine(root.Path, entry.Id, "tvshow.nfo")).Root!;

        // Assert
        episode.Name.LocalName.Should().Be("episodedetails");
        episode.Element("title")!.Value.Should().Be(entry.Identity.EpisodeTitle);
        episode.Element("season")!.Value.Should().Be(entry.Identity.SeasonNumber.ToString());
        episode.Element("episode")!.Value.Should().Be(entry.Identity.EpisodeNumber.ToString());
        series.Element("title")!.Value.Should().Be(entry.Identity.Title);
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveAnUnchangedLinkUntouched(
        ProjectionEntry entry,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        entry.Identity.Kind = MediaKind.Movie;
        entry.StreamPath = Path.Combine(root.Path, entry.Id + ".strm");
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                JellyfinPublicUrl = "https://jellyfin.invalid"
            }
        );
        sut.WriteFiles(entry);
        File.SetLastWriteTimeUtc(entry.StreamPath, File.GetLastWriteTimeUtc(entry.StreamPath).AddMinutes(-1));
        var savedWriteTime = File.GetLastWriteTimeUtc(entry.StreamPath);

        // Act
        sut.WriteFiles(entry);

        // Assert
        File.GetLastWriteTimeUtc(entry.StreamPath).Should().Be(savedWriteTime);
        File.Exists(entry.StreamPath + ".new").Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectNonHttpPlaybackUrls(
        ProjectionEntry entry,
        StreamingOptions configuration,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                JellyfinPublicUrl = "file:///private-media"
            }
        );

        // Act
        var action = () => sut.WriteFiles(entry);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectCredentialsEmbeddedInPlaybackUrls(
        ProjectionEntry entry,
        StreamingOptions configuration,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        var url = $"https://{configuration.TransmissionUsername}:{configuration.TransmissionPassword}@jellyfin.invalid";
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                JellyfinPublicUrl = url
            }
        );

        // Act
        var action = () => sut.WriteFiles(entry);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory, AutoMoqData]
    internal void Should_OmitUnknownProviderIdentifiers(
        ProjectionEntry entry,
        StreamingOptions configuration,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionFiles sut
    )
    {
        // Arrange
        using var root = directory;
        entry.Identity.Kind = MediaKind.Movie;
        entry.Identity.TmdbId = 0;
        entry.Identity.TvdbId = 0;
        entry.Identity.ImdbId = "";
        entry.StreamPath = Path.Combine(root.Path, entry.Id + ".strm");
        options.SetupGet(value => value.CurrentValue).Returns(
            configuration with
            {
                JellyfinPublicUrl = "https://jellyfin.invalid"
            }
        );

        // Act
        sut.WriteFiles(entry);
        var metadata = XDocument.Load(Path.ChangeExtension(entry.StreamPath, ".nfo")).Root!;

        // Assert
        metadata.Elements("uniqueid").Should().BeEmpty("zero identifiers must not merge unrelated Jellyfin entries");
    }
    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        fixture.Freeze<Mock<IOptionsMonitor<StreamingOptions>>>();
        return fixture;
    });
}
