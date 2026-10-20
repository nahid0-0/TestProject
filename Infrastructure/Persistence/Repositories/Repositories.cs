using System.Text.Json;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Common;
using Chess.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chess.Infrastructure.Persistence.Repositories;

public class EfRepository<T>(ChessDbContext db) : IRepository<T> where T : class, IAggregateRoot
{
    protected readonly ChessDbContext Db = db;

    public virtual async Task<T?> GetAsync(Guid id, CancellationToken ct) =>
        await Db.Set<T>().FindAsync([id], ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct) =>
        await Db.Set<T>().AddAsync(entity, ct);

    public virtual void Remove(T entity) =>
        Db.Set<T>().Remove(entity);
}

public sealed class PlayerRepository(ChessDbContext db) : EfRepository<Player>(db), IPlayerRepository
{
    public async Task<Player?> GetByUsernameAsync(string username, CancellationToken ct) =>
        await Db.Players.FirstOrDefaultAsync(p => p.LichessUsername == username.Trim().ToLowerInvariant(), ct);

    public async Task<Player> GetOrCreateOpponentAsync(string username, CancellationToken ct)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var existing = await Db.Players.FirstOrDefaultAsync(p => p.LichessUsername == normalized, ct);
        if (existing is not null) return existing;

        var opponent = Player.Opponent(normalized);
        await Db.Players.AddAsync(opponent, ct);
        return opponent;
    }

    public async Task<IReadOnlyList<Player>> ListTrackedAsync(CancellationToken ct) =>
        await Db.Players.Where(p => p.IsTracked && p.DeletedAt == null).ToListAsync(ct);
}

public sealed class GameRepository(ChessDbContext db) : EfRepository<Game>(db), IGameRepository
{
    public async Task<HashSet<string>> ExistingIdsAsync(IEnumerable<string> lichessIds, CancellationToken ct)
    {
        var idList = lichessIds.ToList();
        var existing = await Db.Games
            .Where(g => idList.Contains(g.LichessGameId))
            .Select(g => g.LichessGameId)
            .ToListAsync(ct);
        return existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct) =>
        await Db.Games.AddRangeAsync(games, ct);

    public IQueryable<Game> Query() => Db.Games.AsNoTracking();
}

public sealed class AnalyticsSnapshotStore(ChessDbContext db) : IAnalyticsSnapshotStore
{
    public async Task<TResult?> TryGetAsync<TResult>(Guid playerId, string type, string filterHash, CancellationToken ct)
        where TResult : class
    {
        var snapshot = await db.AnalyticsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.Type == type && s.FilterHash == filterHash, ct);

        if (snapshot is null) return null;
        return JsonSerializer.Deserialize<TResult>(snapshot.PayloadJson);
    }

    public async Task<object?> TryGetAsync(Guid playerId, string type, string filterHash, Type resultType, CancellationToken ct)
    {
        var snapshot = await db.AnalyticsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.Type == type && s.FilterHash == filterHash, ct);

        if (snapshot is null) return null;
        return JsonSerializer.Deserialize(snapshot.PayloadJson, resultType);
    }

    public async Task SaveAsync(Guid playerId, string type, string filterHash, object result, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(result);
        var existing = await db.AnalyticsSnapshots
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.Type == type && s.FilterHash == filterHash, ct);

        if (existing is not null)
        {
            db.AnalyticsSnapshots.Remove(existing);
        }

        var snapshot = new AnalyticsSnapshot(Guid.NewGuid(), playerId, type, filterHash, json, DateTime.UtcNow);
        await db.AnalyticsSnapshots.AddAsync(snapshot, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task InvalidateAsync(Guid playerId, CancellationToken ct)
    {
        await db.AnalyticsSnapshots
            .Where(s => s.PlayerId == playerId)
            .ExecuteDeleteAsync(ct);
    }
}
