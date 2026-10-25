namespace TransmissionStreaming.Logic.Library;

using Core.Projection;
using Mediator;

/// <summary>Transfers every user's state before a source host entry is retired.</summary>
public static class TransferUserHistory
{
    /// <summary>Requests a merge from one host item into another without deleting either item.</summary>
    public sealed record Command(string SourceItemId, string TargetItemId) : ICommand;

    internal sealed class Handler(IProjectionLibrary library, IMediator mediator) : ICommandHandler<Command>
    {
        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var user in library.GetUserStates(command.SourceItemId, command.TargetItemId))
            {
                if (user.Source is null) continue;
                var merged = await mediator.Send(new MergeWatchState.Query(user.Source, user.Target), cancellationToken).ConfigureAwait(false);
                library.SaveUserState(user.UserId, command.TargetItemId, merged);
            }
            return Unit.Value;
        }
    }
}
