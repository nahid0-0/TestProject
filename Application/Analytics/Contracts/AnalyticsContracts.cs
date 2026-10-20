using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Application.Analytics.Contracts;

public sealed record AnalyticsFilter
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public TimeControlCategory? Category { get; init; }
    public Color? Color { get; init; }
    public bool? RatedOnly { get; init; }
    public int? OpponentRatingMin { get; init; }
    public int? OpponentRatingMax { get; init; }
    public string? Variant { get; init; } = "standard";

    public string Hash() => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
}

[JsonPolymorphic]
public abstract record AnalyticsResult
{
    public int SampleSize { get; init; }
    public bool IsSufficient { get; init; }         // SampleSize >= calculator's minimum
    public double? EvalCoverage { get; init; }      // fraction of games with evals, if relevant
    public DateTime ComputedAt { get; init; } = DateTime.UtcNow;
}

public sealed record PlayerGame(
    Guid GameId,
    DateTime PlayedAt,
    Color Color,
    double Score,
    int? OwnRating,
    int? OpponentRating,
    int? RatingDiff,
    TimeControl TimeControl,
    Termination Termination,
    Guid? OpeningId,
    bool HasEvaluations);

public sealed record AnalyticsContext(Guid PlayerId, AnalyticsFilter Filter);

public interface IAnalyticsCalculator
{
    string Key { get; }
    Type ResultType { get; }
    EnabledModules Module { get; }
    Task<AnalyticsResult> RunAsync(AnalyticsContext ctx, CancellationToken ct);
}

public interface IAnalyticsRegistry
{
    IAnalyticsCalculator Get(string key);   // throws if unknown
    IReadOnlyCollection<string> Keys { get; }
}
