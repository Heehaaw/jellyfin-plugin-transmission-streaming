namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Logic.Library;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using MediaBrowser.Model.IO;
using Mediator;
using Moq;
using Xunit;

public sealed class ImportedFileIgnoreRuleTests
{
    [Theory, AutoMoqData]
    internal void Should_ForwardTheHostFileFactsToThePolicy(FileSystemMetadata source, bool expected,
        [Frozen] Mock<IMediator> mediator, ImportedFileIgnoreRule sut)
    {
        // Arrange
        mediator.Setup(value => value.Send(It.Is<ShouldIgnoreImportedFile.Query>(query =>
            query.Path == source.FullName && query.IsDirectory == source.IsDirectory), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(expected));

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().Be(expected);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
