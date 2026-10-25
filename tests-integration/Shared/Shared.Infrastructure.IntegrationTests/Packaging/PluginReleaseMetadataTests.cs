namespace Shared.Infrastructure.IntegrationTests.Packaging;

using AutoFixture.Xunit2;
using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Shared.Infrastructure.Packaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

public sealed class PluginReleaseMetadataTests
{
    [Theory, AutoMoqData]
    internal void Should_ReadTheAdjacentPackageVersion(MetadataDocument source, ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        files.Write(source);
        var expected = Version.Parse(source.Version!);

        // Act
        var result = sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        result.Should().Be(expected);
    }

    [Theory, AutoMoqData]
    internal void Should_AllowAManualDllWithoutMetadata(ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;

        // Act
        var result = sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_ReturnNoVersionWhenTheMetadataDirectoryIsAbsent(ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        files.RemoveDirectory();

        // Act
        var result = sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        result.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectMetadataWithoutAVersion(MetadataDocument source, ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        source.Version = null;
        files.Write(source);

        // Act
        var action = () => sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        action.Should().Throw<InvalidDataException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectAnInvalidVersion(MetadataDocument source, ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        source.Version += "-invalid";
        files.Write(source);

        // Act
        var action = () => sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        action.Should().Throw<InvalidDataException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectANonStringVersion(NumericMetadataDocument source, ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        files.Write(source);

        // Act
        var action = () => sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        action.Should().Throw<InvalidDataException>();
    }

    [Theory, AutoMoqData]
    internal void Should_RejectANonObjectDocument(int[] source, ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        files.Write(source);

        // Act
        var action = () => sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        action.Should().Throw<InvalidDataException>();
    }

    [Theory, AutoMoqData]
    internal void Should_PropagateMalformedJson(string source, ReleaseMetadataFiles files, PluginReleaseMetadata sut)
    {
        // Arrange
        using var owned = files;
        files.WriteRaw(source);

        // Act
        var action = () => sut.ReadVersion(files.AssemblyFilePath);

        // Assert
        action.Should().Throw<JsonException>();
    }

    public sealed record MetadataDocument
    {
        [JsonPropertyName("version")]
        public string? Version
        {
            get; set;
        }
    }

    public sealed record NumericMetadataDocument(
        [property: JsonPropertyName("version")]
        int Version
    );

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
            fixture.Customize<MetadataDocument>(composer => composer.With(value => value.Version, () => fixture.Create<Version>().ToString()));
            return fixture;
        }
    );
}
