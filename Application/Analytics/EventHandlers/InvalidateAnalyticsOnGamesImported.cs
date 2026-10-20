using Chess.Application.Contracts.Events;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Events;

namespace Chess.Application.Analytics.EventHandlers;

public sealed class InvalidateAnalyticsOnGamesImported(IAnalyticsSnapshotStore store)
    : IEventHandler<GamesImported>
{
    public Task HandleAsync(GamesImported e, CancellationToken ct) =>
        store.InvalidateAsync(e.PlayerId, ct);
}
