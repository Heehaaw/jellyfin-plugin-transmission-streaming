namespace TransmissionStreaming.IntegrationTests.Infrastructure.Persistence;

using AutoFixture;
using AutoFixture.AutoMoq;
using global::TransmissionStreaming.TestSupport.Testing;
using Filesystem;
using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using global::TransmissionStreaming.Core.Projection;
using Microsoft.Data.Sqlite;
using global::TransmissionStreaming.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ProjectionEntry = global::TransmissionStreaming.Core.Projection.Projection;

public sealed class ProjectionStoreTests
{
    private const int HistoryLimit = 20;

    [Theory, AutoMoqData]
    internal void Should_RestoreTheCompleteProjectionAcrossStoreInstances(
        ProjectionEntry source,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionStore sut
    )
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
        var imported = source with
        {
            CompletedPath = importedPath,
            Status = "Complete",
            Progress = 1
        };

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

    [Theory, AutoMoqData]
    internal void Should_RemoveOnlyTheExactIdAndAllowRepeatedRemoval(ProjectionEntry source, TemporaryDirectory directory, ProjectionStore sut)
    {
        // Arrange
        using var root = directory;
        var retained = source with
        {
            Id = source.Id + "-retained"
        };
        source.Id += "' OR 1=1 --";
        sut.Save(source);
        sut.Save(retained);

        // Act
        sut.Remove(source.Id);
        sut.Remove(source.Id);

        // Assert
        sut.Get(source.Id).Should().BeNull();
        sut.All().Should().ContainSingle(entry => entry.Id == retained.Id);
    }

    [Theory, AutoMoqData]
    internal void Should_ArchiveTheHandoffAndRetireOnlyItsActiveProjection(
        ProjectionEntry active,
        CompletedProjection source,
        TemporaryDirectory directory,
        ProjectionStore sut
    )
    {
        // Arrange
        using var root = directory;
        source = source with
        {
            Id = active.Id
        };
        var retained = active with
        {
            Id = active.Id + "-retained"
        };
        sut.Save(active);
        sut.Save(retained);

        // Act
        sut.Complete(source, HistoryLimit);

        // Assert
        sut.Get(active.Id).Should().BeNull();
        sut.All().Should().ContainSingle(entry => entry.Id == retained.Id);
        sut.Completed().Should().Equal(source);
    }

    [Theory, AutoMoqData]
    internal void Should_RestoreCompletionHistoryAfterReopening(
        CompletedProjection source,
        TemporaryDirectory directory,
        Mock<IOptionsMonitor<StreamingOptions>> options,
        ProjectionStore sut
    )
    {
        // Arrange
        using var root = directory;
        sut.Complete(source, HistoryLimit);

        // Act
        var reopened = new ProjectionStore(options.Object);
        var result = reopened.Completed();

        // Assert
        result.Should().Equal(source);
    }

    [Theory, AutoMoqData]
    internal void Should_UpsertCompletionRetriesWithoutDuplicatingHistory(CompletedProjection source, TemporaryDirectory directory, ProjectionStore sut)
    {
        // Arrange
        using var root = directory;
        sut.Complete(source, HistoryLimit);
        var updated = source with
        {
            Title = source.Title + " updated",
            CompletedAt = source.CompletedAt.AddMinutes(1)
        };

        // Act
        sut.Complete(source, HistoryLimit);
        sut.Complete(updated, HistoryLimit);

        // Assert
        sut.Completed().Should().Equal(updated);
    }

    [Theory, AutoMoqData]
    internal void Should_RetainOnlyTheNewestBoundedHistoryInUtcOrder(
        Generator<CompletedProjection> source,
        TemporaryDirectory directory,
        ProjectionStore sut
    )
    {
        // Arrange
        using var root = directory;
        var entries = source.Take(HistoryLimit + 3)
            .Select((entry, index) => entry with
            {
                CompletedAt = entry.CompletedAt.ToOffset(TimeSpan.FromHours(index % 2 == 0 ? 13 : -11))
            }
            ).ToArray();
        var expected = entries.OrderByDescending(entry => entry.CompletedAt)
            .ThenByDescending(entry => entry.Id, StringComparer.Ordinal).Take(HistoryLimit).ToArray();

        // Act
        foreach (var entry in entries)
        {
            sut.Complete(entry, HistoryLimit);
        }

        var result = sut.Completed();

        // Assert
        result.Should().Equal(expected);
    }

    [Theory, AutoMoqData]
    internal void Should_RollBackHistoryWhenActiveRetirementFails(
        ProjectionEntry active,
        CompletedProjection source,
        TemporaryDirectory directory,
        ProjectionStore sut
    )
    {
        // Arrange
        using var root = directory;
        source = source with
        {
            Id = active.Id
        };
        sut.Save(active);
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(root.Path, "projections.db")
            }.ToString()
        );
        connection.Open();
        using var trigger = connection.CreateCommand();
        trigger.CommandText = "CREATE TRIGGER refuse_retirement BEFORE DELETE ON projections BEGIN SELECT RAISE(ABORT, 'Retirement unavailable'); END;";
        trigger.ExecuteNonQuery();

        // Act
        var action = () => sut.Complete(source, HistoryLimit);

        // Assert
        action.Should().Throw<SqliteException>();
        sut.Get(active.Id).Should().NotBeNull();
        sut.Completed().Should().BeEmpty();
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
            fixture.Register(() => new Generator<CompletedProjection>(fixture));
            var directory = fixture.Freeze<TemporaryDirectory>();
            fixture.Customize<StreamingOptions>(composer => composer.With(value => value.StateDirectory, directory.Path));
            fixture.Customize<Mock<IOptionsMonitor<StreamingOptions>>>(composer => composer.Do(mock =>
                    mock.SetupGet(value => value.CurrentValue).Returns(fixture.Create<StreamingOptions>())
                )
            );
            fixture.Freeze<Mock<IOptionsMonitor<StreamingOptions>>>();
            return fixture;
        }
    );
}
