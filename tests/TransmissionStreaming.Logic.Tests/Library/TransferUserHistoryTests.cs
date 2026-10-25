namespace TransmissionStreaming.Logic.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Mediator;
using Moq;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Logic.Library;
using Xunit;

public sealed class TransferUserHistoryTests
{
    [Theory, AutoMoqData]
    internal async Task Should_MergeEachAvailableUserState(
        TransferUserHistory.Command source,
        LibraryUserState[] users,
        LibraryWatchState merged,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        TransferUserHistory.Handler sut
    )
    {
        // Arrange
        library.Setup(value => value.GetUserStates(source.SourceItemId, source.TargetItemId)).Returns(users);
        mediator.Setup(value => value.Send(It.IsAny<MergeWatchState.Query>(), It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(merged));

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        foreach (var user in users)
        {
            mediator.Verify(
                expression: value => value.Send(It.Is<MergeWatchState.Query>(query => query.Source == user.Source && query.Target == user.Target), It.IsAny<CancellationToken>()),
                times: Times.Once
            );
            library.Verify(value => value.SaveUserState(user.UserId, source.TargetItemId, merged), Times.Once);
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_SkipUsersWithoutSourceHistory(
        TransferUserHistory.Command source,
        LibraryUserState user,
        [Frozen] Mock<IProjectionLibrary> library,
        [Frozen] Mock<IMediator> mediator,
        TransferUserHistory.Handler sut
    )
    {
        // Arrange
        library.Setup(value => value.GetUserStates(source.SourceItemId, source.TargetItemId)).Returns(
            [
                user with
                {
                    Source = null
                }
            ]
        );

        // Act
        await sut.Handle(source, CancellationToken.None);

        // Assert
        mediator.Verify(value => value.Send(It.IsAny<MergeWatchState.Query>(), It.IsAny<CancellationToken>()), Times.Never);
        library.Verify(value => value.SaveUserState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LibraryWatchState>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 2
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            return fixture;
        }
    );
}
