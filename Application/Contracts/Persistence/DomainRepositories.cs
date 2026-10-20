using Chess.Domain.Entities;

namespace Chess.Application.Contracts.Persistence;

public interface IPlayerRepository : IRepository<Player>
{
    Task<Player?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<Player> GetOrCreateOpponentAsync(string username, CancellationToken ct);
    Task<IReadOnlyList<Player>> ListTrackedAsync(CancellationToken ct);
}

public interface IGameRepository : IRepository<Game>
{
    Task<HashSet<string>> ExistingIdsAsync(IEnumerable<string> lichessIds, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct);
    IQueryable<Game> Query();     // read side, AsNoTracking
}

public interface IAnalyticsSnapshotStore
{
    Task<TResult?> TryGetAsync<TResult>(Guid playerId, string type, string filterHash, CancellationToken ct)
        where TResult : class;
    Task<object?> TryGetAsync(Guid playerId, string type, string filterHash, Type resultType, CancellationToken ct);
    Task SaveAsync(Guid playerId, string type, string filterHash, object result, CancellationToken ct);
    Task InvalidateAsync(Guid playerId, CancellationToken ct);
}
