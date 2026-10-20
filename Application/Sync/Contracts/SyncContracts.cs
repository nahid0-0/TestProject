using Chess.Domain.Enums;

namespace Chess.Application.Sync.Contracts;

public interface ISyncScheduler
{
    ValueTask EnqueueAsync(Guid playerId, SyncMode mode, CancellationToken ct = default);
}

public interface IPlayerSyncLock
{
    // Postgres: SELECT pg_try_advisory_lock(hashtext(@key)); returns null if already held.
    Task<IAsyncDisposable?> TryAcquireAsync(Guid playerId, CancellationToken ct);
}

public sealed record SyncRequest(Guid PlayerId, SyncMode Mode);

public sealed class SyncOptions
{
    public int MaxConcurrentSyncs { get; set; } = 4;
    public TimeSpan ScheduledInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromHours(12);
}
