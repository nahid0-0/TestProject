using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Application.Contracts.External;

public sealed record GameQuery(
    long? SinceMs = null,
    long? UntilMs = null,
    int? Max = null,
    bool? Rated = null,
    string? PerfType = null,
    bool Clocks = true,
    bool Evals = true,
    bool Opening = true);

public sealed record RawGame(
    string Id,
    bool? Rated = null,
    string? Variant = null,
    string? Speed = null,
    string? Perf = null,
    long? CreatedAt = null,
    long? LastMoveAt = null,
    string? Status = null,
    RawPlayers? Players = null,
    RawOpening? Opening = null,
    string? Moves = null,
    IReadOnlyList<int>? Clocks = null,
    IReadOnlyList<RawEval>? Evals = null,
    string? Winner = null);

public sealed record RawPlayers(RawPlayer? White, RawPlayer? Black);

public sealed record RawPlayer(RawUser? User, int? Rating, int? RatingDiff, bool? Provisional);

public sealed record RawUser(string? Name, string? Id);

public sealed record RawOpening(string? Eco, string? Name, int? Ply);

public sealed record RawEval(int? Cp, int? Mate);

public sealed record ParsedGame(
    string LichessGameId,
    string WhiteUsername,
    string BlackUsername,
    GameResult Result,
    Termination Termination,
    TimeControl TimeControl,
    bool Rated,
    DateTime PlayedAt,
    long TimestampMs,
    IReadOnlyList<Move> Moves,
    int? WhiteRating = null,
    int? BlackRating = null,
    int? WhiteRatingDiff = null,
    int? BlackRatingDiff = null,
    string? EcoCode = null,
    string? OpeningName = null,
    string Variant = "standard");

public sealed record LichessProfile(
    string Id,
    string Username,
    string? Title = null,
    bool? Online = null,
    DateTime? CreatedAt = null,
    DateTime? SeenAt = null);

public sealed record RatingPoint(
    DateTime Date,
    int Rating,
    TimeControlCategory Category);

public sealed class AccuracyOptions
{
    public int BlunderThresholdCentipawns { get; set; } = 300;
    public int MistakeThresholdCentipawns { get; set; } = 100;
    public int InaccuracyThresholdCentipawns { get; set; } = 50;
}
