namespace HdrPlayback.IntegrationTests.Packaging;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Testing;
using TransmissionStreaming.Packaging;
using Xunit;

public sealed class PluginSelectionTests
{
    [Theory, AutoMoqData]
    internal void Should_SelectHdrProjectAndVersion(Version source)
    {
        // Arrange
        var tag = "hdr-v" + source;
        string[] arguments = ["--plugin", "hdr-playback", "--release-tag", tag];
        var root = Path.GetTempPath();

        // Act
        var options = PackageOptions.Parse(arguments);
        var layout = new RepositoryLayout(root)
        {
            Plugin = options.Plugin
        };

        // Assert
        layout.ProjectPath.Should().Be(Path.Combine(root, "src", "HdrPlayback", "Jellyfin.Plugin.HdrPlayback", "Jellyfin.Plugin.HdrPlayback.csproj"));
        options.ReleaseVersion.Should().Be(source);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectHdrTagForTransmission(Version source)
    {
        // Arrange
        string[] arguments = ["--release-tag", "hdr-v" + source];

        // Act
        var action = () => PackageOptions.Parse(arguments).ReleaseVersion;

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveLegacyTransmissionSelection(string source)
    {
        // Arrange
        string[] arguments = [];

        // Act
        var options = PackageOptions.Parse(arguments);
        var layout = new RepositoryLayout(source)
        {
            Plugin = options.Plugin
        };

        // Assert
        var expectedProject = Path.Combine(source, "src", "TransmissionStreaming", "Jellyfin.Plugin.TransmissionStreaming", "Jellyfin.Plugin.TransmissionStreaming.csproj");
        layout.ProjectPath.Should().Be(expectedProject);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture
        {
            RepeatCount = 1
        };
        fixture.Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true
        });
        fixture.Register(() => new Version(fixture.Create<byte>(), fixture.Create<byte>(), fixture.Create<byte>(), fixture.Create<byte>()));
        return fixture;
    }
    );
}
