namespace TransmissionStreaming.IntegrationTests.Packaging;

using global::TransmissionStreaming.Packaging;
using global::TransmissionStreaming.TestSupport.Testing;
using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Xunit;

public sealed class PackageOptionsTests
{
    [Theory, AutoMoqData]
    internal void Should_ReadANumericReleaseTag(PackageOptions source)
    {
        // Arrange
        var expected = Version.Parse(source.ReleaseTag!);

        // Act
        var result = source.ReleaseVersion;

        // Assert
        result.Should().Be(expected);
    }

    [Theory, AutoMoqData]
    internal void Should_NormalizeAPrefixedTwoComponentTag(PackageOptions source)
    {
        // Arrange
        var version = Version.Parse(source.ReleaseTag!);
        source = source with
        {
            ReleaseTag = "v" + version.ToString(2)
        };
        var expected = new Version(version.Major, version.Minor, 0, 0);

        // Act
        var result = source.ReleaseVersion;

        // Assert
        result.Should().Be(expected);
    }

    [Theory, AutoMoqData]
    internal void Should_NormalizeAMissingRevision(PackageOptions source)
    {
        // Arrange
        var version = Version.Parse(source.ReleaseTag!);
        source = source with
        {
            ReleaseTag = version.ToString(3)
        };
        var expected = new Version(version.Major, version.Minor, version.Build, 0);

        // Act
        var result = source.ReleaseVersion;

        // Assert
        result.Should().Be(expected);
    }

    [Theory, AutoMoqData]
    internal void Should_AllowAnAbsentReleaseTag(PackageOptions source)
    {
        // Arrange
        source = source with
        {
            ReleaseTag = null
        };

        // Act
        var result = source.ReleaseVersion;

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAMalformedReleaseTag(PackageOptions source)
    {
        // Arrange
        source = source with
        {
            ReleaseTag = source.ReleaseTag + "-invalid"
        };

        // Act
        var action = () => source.ReleaseVersion;

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_AcceptEquivalentNormalizedVersions(PackageOptions source)
    {
        // Arrange
        var version = Version.Parse(source.ReleaseTag!);
        source = source with
        {
            ReleaseTag = "v" + version.ToString(2)
        };
        var metadataVersion = new Version(version.Major, version.Minor, 0, 0).ToString();

        // Act
        var action = () => source.ValidateVersion(metadataVersion);

        // Assert
        action.Should().NotThrow();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectMetadataThatDiffersFromTheReleaseTag(PackageOptions source)
    {
        // Arrange
        var version = Version.Parse(source.ReleaseTag!);
        var mismatchedVersion = new Version(version.Major + 1, version.Minor, version.Build, version.Revision).ToString();

        // Act
        var action = () => source.ValidateVersion(mismatchedVersion);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_SkipReleaseValidationWithoutATag(PackageOptions source)
    {
        // Arrange
        var unversionedMetadata = source.ReleaseTag + "-development";
        source = source with
        {
            ReleaseTag = null
        };

        // Act
        var action = () => source.ValidateVersion(unversionedMetadata);

        // Assert
        action.Should().NotThrow();
    }

    [Theory, AutoMoqData]
    internal void Should_ParseAllSuppliedOptions(PackageOptions source)
    {
        // Arrange
        string[] arguments =
        [
            "--archive", source.ArchivePath!, "--source-url", source.SourceUrl!.AbsoluteUri,
            "--previous-manifest-url", source.PreviousManifestUrl!.AbsoluteUri, "--release-tag", source.ReleaseTag!
        ];
        var expectedArchive = Path.GetFullPath(source.ArchivePath!);

        // Act
        var result = PackageOptions.Parse(arguments);

        // Assert
        result.SourceUrl.Should().Be(source.SourceUrl);
        result.PreviousManifestUrl.Should().Be(source.PreviousManifestUrl);
        result.ReleaseTag.Should().Be(source.ReleaseTag);
        result.ArchivePath.Should().Be(expectedArchive);
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAnOptionWithoutAValue(PackageOptions source)
    {
        // Arrange
        string[] arguments = ["--release-tag", source.ReleaseTag!, "--archive"];

        // Act
        var action = () => PackageOptions.Parse(arguments);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAnUnknownOption(PackageOptions source)
    {
        // Arrange
        string[] arguments = ["--" + source.ReleaseTag, source.ArchivePath!];

        // Act
        var action = () => PackageOptions.Parse(arguments);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAnInsecurePackageUrl(PackageOptions source)
    {
        // Arrange
        var insecureUrl = source.SourceUrl!.AbsoluteUri.Replace("https://", "http://", StringComparison.Ordinal);
        string[] arguments = ["--source-url", insecureUrl];

        // Act
        var action = () => PackageOptions.Parse(arguments);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAQueriedArchiveUrl(PackageOptions source)
    {
        // Arrange
        var queriedUrl = source.SourceUrl!.AbsoluteUri + "?" + source.ReleaseTag;
        string[] arguments = ["--source-url", queriedUrl];

        // Act
        var action = () => PackageOptions.Parse(arguments);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAManifestWithoutAPackageUrl(PackageOptions source)
    {
        // Arrange
        string[] arguments = ["--previous-manifest-url", source.PreviousManifestUrl!.AbsoluteUri];

        // Act
        var action = () => PackageOptions.Parse(arguments);

        // Assert
        action.Should().Throw<ArgumentException>();
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
            fixture.Register(() => new Version(fixture.Create<byte>(), fixture.Create<byte>(), fixture.Create<byte>(), fixture.Create<byte>()));
            fixture.Register(() => new Uri($"https://packages.example/{fixture.Create<Guid>()}.zip"));
            fixture.Customize<PackageOptions>(composer => composer.With(value => value.ReleaseTag, () => fixture.Create<Version>().ToString()));
            return fixture;
        }
    );
}
