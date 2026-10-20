using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Domain.Entities;

public sealed record GameDraft(
    string LichessGameId,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    GameResult Result,
    Termination Termination,
    TimeControl TimeControl,
    bool Rated,
    DateTime PlayedAt,
    IEnumerable<Move> Moves,
    int? WhiteRating = null,
    int? BlackRating = null,
    int? WhiteRatingDiff = null,
    int? BlackRatingDiff = null,
    Guid? OpeningId = null,
    string Variant = "standard");
