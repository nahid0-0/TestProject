using Chess.Domain.Common;
using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Domain.Entities;

public sealed class Game : Entity<Guid>, IAggregateRoot
{
    private readonly List<Move> _moves = new();
    private Game() { }

    public string LichessGameId { get; private set; } = "";
    public Guid WhitePlayerId { get; private set; }
    public Guid BlackPlayerId { get; private set; }
    public GameResult Result { get; private set; }
    public Termination Termination { get; private set; }
    public TimeControl TimeControl { get; private set; }
    public string Variant { get; private set; } = "standard";
    public bool Rated { get; private set; }
    public DateTime PlayedAt { get; private set; }
    public int? WhiteRating { get; private set; }
    public int? BlackRating { get; private set; }
    public int? WhiteRatingDiff { get; private set; }
    public int? BlackRatingDiff { get; private set; }
    public Guid? OpeningId { get; private set; }
    public bool HasEvaluations { get; private set; }
    public IReadOnlyList<Move> Moves => _moves;

    public static Game Create(GameDraft d)
    {
        var g = new Game
        {
            Id = Guid.NewGuid(),
            LichessGameId = d.LichessGameId,
            WhitePlayerId = d.WhitePlayerId,
            BlackPlayerId = d.BlackPlayerId,
            Result = d.Result,
            Termination = d.Termination,
            TimeControl = d.TimeControl,
            Variant = d.Variant,
            Rated = d.Rated,
            PlayedAt = d.PlayedAt,
            WhiteRating = d.WhiteRating,
            BlackRating = d.BlackRating,
            WhiteRatingDiff = d.WhiteRatingDiff,
            BlackRatingDiff = d.BlackRatingDiff,
            OpeningId = d.OpeningId
        };
        g.SetMoves(d.Moves);
        return g;
    }

    public void SetMoves(IEnumerable<Move> moves)
    {
        _moves.Clear();
        _moves.AddRange(moves.OrderBy(m => m.Ply));
        for (int i = 0; i < _moves.Count; i++)
        {
            if (_moves[i].Ply != i + 1)
                throw new DomainException($"Non-contiguous ply at index {i} in {LichessGameId}");
        }
        HasEvaluations = _moves.Any(m => m.Evaluation is not null);
    }

    public Color? ColorOf(Guid playerId) =>
        playerId == WhitePlayerId ? Color.White : playerId == BlackPlayerId ? Color.Black : null;

    // Score from a given player's perspective: 1, 0.5, 0
    public double ScoreFor(Guid playerId) => (Result, ColorOf(playerId)) switch
    {
        (GameResult.Draw, _) => 0.5,
        (GameResult.WhiteWin, Color.White) => 1,
        (GameResult.BlackWin, Color.Black) => 1,
        (GameResult.Unknown, _) => 0.5,
        _ => 0
    };
}
