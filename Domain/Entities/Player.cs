using Chess.Domain.Common;
using Chess.Domain.Enums;
using Chess.Domain.Events;

namespace Chess.Domain.Entities;

public sealed class Player : Entity<Guid>, IAggregateRoot
{
    private Player() { }   // EF

    public string LichessUsername { get; private set; } = "";
    public string? DisplayName { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public long? LastGameTimestampMs { get; private set; }  // cursor for incremental sync
    public bool IsTracked { get; private set; }             // false = opponent placeholder
    public EnabledModules Modules { get; private set; } = EnabledModules.All;
    public DateTime? DeletedAt { get; private set; }        // soft delete

    public static Player Track(string username)
    {
        var p = new Player
        {
            Id = Guid.NewGuid(),
            LichessUsername = username.Trim().ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow,
            IsTracked = true
        };
        p.Raise(new PlayerRegistered(p.Id, p.LichessUsername));
        return p;
    }

    public static Player Opponent(string username) => new()
    {
        Id = Guid.NewGuid(),
        LichessUsername = username.Trim().ToLowerInvariant(),
        CreatedAt = DateTime.UtcNow,
        IsTracked = false
    };

    public void PromoteToTracked()
    {
        IsTracked = true;
        Raise(new PlayerRegistered(Id, LichessUsername));
    }

    public void AdvanceCursor(long lastGameTimestampMs)
    {
        if (lastGameTimestampMs > (LastGameTimestampMs ?? 0))
            LastGameTimestampMs = lastGameTimestampMs;
        LastSyncedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        IsTracked = false;
    }
}
