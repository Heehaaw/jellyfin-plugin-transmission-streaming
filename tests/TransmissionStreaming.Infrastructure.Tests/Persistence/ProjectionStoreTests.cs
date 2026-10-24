namespace TransmissionStreaming.Infrastructure.Tests.Persistence;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using Filesystem;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Infrastructure.Persistence;
using Moq;
using Xunit;
using ProjectionEntry = global::TransmissionStreaming.Core.Projection.Projection;

public sealed class ProjectionStoreTests
{
    [Theory, AutoMoqData]
    internal void Should_RestoreTheCompleteProjectionAcrossStoreInstances(ProjectionEntry source, TemporaryDirectory directory,
        [Frozen] Mock<IStreamingOptionsProvider> options, ProjectionStore sut)
    {
        // Arrange
        using var root = directory;
        sut.Save(source);

        // Act
        var reopened = new ProjectionStore(options.Object);
        var restored = reopened.Get(source.Id);

        // Assert
        restored.Should().BeEquivalentTo(source, comparison => comparison.Excluding(value => value.Secret));
        string.Equals(restored!.Secret, source.Secret, StringComparison.Ordinal).Should().BeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(Directory.GetFiles(root.Path, "*.db").Single()).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Theory, AutoMoqData]
    internal void Should_UpdateWithoutDuplicatingThePermanentIdentity(ProjectionEntry source, TemporaryDirectory directory, ProjectionStore sut)
    {
        // Arrange
        using var root = directory;
        var importedPath = source.CompletedPath;
        source.CompletedPath = null;
        sut.Save(source);
        var imported = source with { CompletedPath = importedPath, Status = "Complete", Progress = 1 };

        // Act
        sut.Save(imported);
        var records = sut.All();

        // Assert
        records.Should().ContainSingle();
        records[0].Should().BeEquivalentTo(imported, comparison => comparison.Excluding(value => value.Secret));
        string.Equals(records[0].Secret, source.Secret, StringComparison.Ordinal).Should().BeTrue();
    }

    [Theory, AutoMoqData]
    internal void Should_TreatLookupIdsAsData(ProjectionEntry source, TemporaryDirectory directory, ProjectionStore sut)
    {
        // Arrange
        using var root = directory;
        sut.Save(source);
        var unknown = source.Id + "' OR 1=1 --";

        // Act
        var result = sut.Get(unknown);

        // Assert
        result.Should().BeNull();
        sut.All().Should().ContainSingle();
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        var directory = fixture.Freeze<TemporaryDirectory>();
        fixture.Customize<StreamingOptions>(composer => composer.With(value => value.StateDirectory, directory.Path));
        fixture.Customize<Mock<IStreamingOptionsProvider>>(composer => composer.Do(mock =>
            mock.Setup(value => value.Get()).Returns(fixture.Create<StreamingOptions>())));
        return fixture;
    });
}
