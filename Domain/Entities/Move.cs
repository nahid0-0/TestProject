using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Domain.Entities;

public sealed class Move
{
    public Guid GameId { get; init; }
    public int Ply { get; init; }                 // 1-based; odd = White
    public string San { get; init; } = "";
    public string Uci { get; init; } = "";
    public string? FenAfter { get; init; }
    public int? ClockRemainingMs { get; init; }
    public Evaluation? Evaluation { get; init; }
    public MoveClassification? Classification { get; set; }
    public GamePhase Phase { get; set; }
    public Color Mover => Ply % 2 == 1 ? Color.White : Color.Black;
}
