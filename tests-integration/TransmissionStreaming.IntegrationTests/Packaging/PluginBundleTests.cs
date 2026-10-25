namespace TransmissionStreaming.IntegrationTests.Packaging;

using global::TransmissionStreaming.Packaging;
using global::TransmissionStreaming.TestSupport.Testing;
using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Xunit;

public sealed class PluginBundleTests
{
    [Theory, AutoMoqData]
    internal async Task Should_StampReleaseMetadataAndPreserveEveryAssembly(
        PluginDescriptor source,
        byte[] payload,
        PackageFiles files
    )
    {
        // Arrange
        using var owned = files;
        var sourcePath = files.CreateArchive(source, payload);
        var expectedHashes = files.ReadMemberHashes(sourcePath);
        var originalVersion = Version.Parse(source.Version);
        var release = source with
        {
            Version = $"{originalVersion.Major + 1}.{originalVersion.Minor}.{originalVersion.Build}.{originalVersion.Revision}"
        };
        var metadataPath = files.WriteMetadata(release);
        var expectedPath = Path.Combine(files.OutputDirectory, release.ArchiveName);

        // Act
        var result = await PluginBundle.RepackAsync(sourcePath, metadataPath, release, files.OutputDirectory, CancellationToken.None);
        var descriptor = await PluginBundle.ReadDescriptorAsync(result, CancellationToken.None);
        var resultHashes = files.ReadMemberHashes(result);

        // Assert
        result.Should().Be(expectedPath);
        descriptor.Should().BeEquivalentTo(release);
        resultHashes.Keys.Should().BeEquivalentTo(source.Assemblies.Append(PluginDescriptor.FileName));
        foreach (var assembly in source.Assemblies)
        {
            resultHashes[assembly].Should().Be(expectedHashes[assembly], "repacking must reuse the verified member {0}", assembly);
        }

        resultHashes[PluginDescriptor.FileName].Should().NotBe(expectedHashes[PluginDescriptor.FileName]);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RepackAnArchiveAlreadyAtItsFinalPath(PluginDescriptor source, byte[] payload, PackageFiles files)
    {
        // Arrange
        using var owned = files;
        var sourcePath = files.CreateArchive(source, payload);
        var metadataPath = files.WriteMetadata(source);
        var finalPath = await PluginBundle.RepackAsync(sourcePath, metadataPath, source, files.OutputDirectory, CancellationToken.None);
        var expectedHashes = files.ReadMemberHashes(finalPath);

        // Act
        var result = await PluginBundle.RepackAsync(finalPath, metadataPath, source, files.OutputDirectory, CancellationToken.None);
        var resultHashes = files.ReadMemberHashes(result);

        // Assert
        result.Should().Be(finalPath);
        resultHashes.Should().BeEquivalentTo(expectedHashes);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAnArtifactWithAMissingAssembly(PluginDescriptor source, byte[] payload, PackageFiles files)
    {
        // Arrange
        using var owned = files;
        var missingAssembly = source.Assemblies[0];
        var sourcePath = files.CreateArchive(source, payload, omittedAssembly: missingAssembly);
        var metadataPath = files.WriteMetadata(source);
        var destinationPath = Path.Combine(files.OutputDirectory, source.ArchiveName);

        // Act
        var action = () => PluginBundle.RepackAsync(sourcePath, metadataPath, source, files.OutputDirectory, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*" + missingAssembly + "*");
        File.Exists(destinationPath).Should().BeFalse();
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
            fixture.Customize<PluginDescriptor>(composer => composer
                .With(value => value.Version, () => fixture.Create<Version>().ToString())
                .With(value => value.TargetAbi, () => fixture.Create<Version>().ToString())
                .With(value => value.Assemblies, () => fixture.CreateMany<Guid>(2).Select(value => value + ".dll").ToArray()));
            return fixture;
        }
    );
}
