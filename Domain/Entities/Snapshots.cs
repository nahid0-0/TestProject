using Chess.Domain.Common;
using Chess.Domain.Enums;

namespace Chess.Domain.Entities;

public sealed class RatingSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PlayerId { get; init; }
    public TimeControlCategory Category { get; init; }
    public int Rating { get; init; }
    public DateTime RecordedAt { get; init; }
}

public sealed class AnalyticsSnapshot : Entity<Guid>
{
    public Guid PlayerId { get; init; }
    public string Type { get; init; } = "";       // calculator Key
    public string FilterHash { get; init; } = ""; // hash of serialized filter
    public string PayloadJson { get; init; } = "";
    public DateTime ComputedAt { get; init; }

    public AnalyticsSnapshot() { }

    public AnalyticsSnapshot(Guid id, Guid playerId, string type, string filterHash, string payloadJson, DateTime computedAt)
    {
        Id = id;
        PlayerId = playerId;
        Type = type;
        FilterHash = filterHash;
        PayloadJson = payloadJson;
        ComputedAt = computedAt;
    }
}
