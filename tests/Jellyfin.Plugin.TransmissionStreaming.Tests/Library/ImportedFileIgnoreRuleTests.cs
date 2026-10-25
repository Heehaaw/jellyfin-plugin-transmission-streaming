namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Logic.Library;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using MediaBrowser.Model.IO;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.TransmissionStreaming.Tests.Hosting;
using Moq;
using Xunit;

public sealed class ImportedFileIgnoreRuleTests
{
    [Theory, AutoMoqData]
    internal void Should_ForwardTheHostFileFactsToThePolicy(
        FileSystemMetadata source,
        bool expected,
        [Frozen] Mock<IMediator> mediator,
        [Frozen] Mock<IServiceScopeFactory> scopes,
        [Frozen] Mock<IServiceScope> scope,
        ImportedFileIgnoreRule sut
    )
    {
        // Arrange
        mediator.Setup(value => value.Send(
                query: It.Is<ShouldIgnoreImportedFile.Query>(query => query.Path == source.FullName && query.IsDirectory == source.IsDirectory),
                cancellationToken: It.IsAny<CancellationToken>()
            )
        ).Returns(ValueTask.FromResult(expected));

        // Act
        var result = sut.ShouldIgnore(source, null);

        // Assert
        result.Should().Be(expected);
        scopes.Verify(value => value.CreateScope(), Times.Once);
        scope.Verify(value => value.Dispose(), Times.Once);
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
            fixture.Customize(new ScopedMediatorCustomization());
            return fixture;
        }
    );
}
