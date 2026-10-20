using Chess.Application.Contracts.External;
using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Chess.Infrastructure.Lichess;

public sealed class LichessGameParser : IGameParser
{
    public ParsedGame Parse(RawGame raw)
    {
        var whiteUser = raw.Players?.White?.User?.Id ?? raw.Players?.White?.User?.Name ?? "white_unknown";
        var blackUser = raw.Players?.Black?.User?.Id ?? raw.Players?.Black?.User?.Name ?? "black_unknown";

        var result = (raw.Status, raw.Winner) switch
        {
            (_, "white") => GameResult.WhiteWin,
            (_, "black") => GameResult.BlackWin,
            ("draw", _) or ("stalemate", _) => GameResult.Draw,
            _ => GameResult.Unknown
        };

        var termination = (raw.Status?.ToLowerInvariant()) switch
        {
            "mate" => Termination.Checkmate,
            "resign" => Termination.Resign,
            "outoftime" => Termination.Timeout,
            "timeout" => Termination.Timeout,
            "stalemate" => Termination.Stalemate,
            "draw" => Termination.Agreement,
            _ => Termination.Other
        };

        var movesStr = raw.Moves ?? "";
        var tokens = movesStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var moves = new List<Move>();

        for (int i = 0; i < tokens.Length; i++)
        {
            int? clock = (raw.Clocks is not null && i < raw.Clocks.Count) ? raw.Clocks[i] * 10 : null;
            Evaluation? ev = null;
            if (raw.Evals is not null && i < raw.Evals.Count)
            {
                var rawEval = raw.Evals[i];
                ev = new Evaluation(rawEval.Cp, rawEval.Mate);
            }

            moves.Add(new Move
            {
                Ply = i + 1,
                Uci = tokens[i],
                San = tokens[i],
                ClockRemainingMs = clock,
                Evaluation = ev
            });
        }

        var playedAt = raw.CreatedAt.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(raw.CreatedAt.Value).UtcDateTime
            : DateTime.UtcNow;

        var tc = new TimeControl(180, 2); // Default blitz if not provided in raw

        return new ParsedGame(
            LichessGameId: raw.Id,
            WhiteUsername: whiteUser,
            BlackUsername: blackUser,
            Result: result,
            Termination: termination,
            TimeControl: tc,
            Rated: raw.Rated ?? true,
            PlayedAt: playedAt,
            TimestampMs: raw.CreatedAt ?? raw.LastMoveAt ?? 0,
            Moves: moves,
            WhiteRating: raw.Players?.White?.Rating,
            BlackRating: raw.Players?.Black?.Rating,
            WhiteRatingDiff: raw.Players?.White?.RatingDiff,
            BlackRatingDiff: raw.Players?.Black?.RatingDiff,
            EcoCode: raw.Opening?.Eco,
            OpeningName: raw.Opening?.Name,
            Variant: raw.Variant ?? "standard");
    }
}

public sealed class TrieOpeningClassifier : IOpeningClassifier
{
    private sealed class TrieNode
    {
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Opening? Opening { get; set; }
    }

    private readonly TrieNode _root = new();

    public TrieOpeningClassifier()
    {
        // Seed default foundational openings
        Register(new Opening(Guid.NewGuid(), "B00", "King's Pawn Opening", null, "e2e4"));
        Register(new Opening(Guid.NewGuid(), "B20", "Sicilian Defense", null, "e2e4 c7c5"));
        Register(new Opening(Guid.NewGuid(), "C60", "Ruy Lopez", null, "e2e4 e7e5 g1f3 b8c6 f1b5"));
        Register(new Opening(Guid.NewGuid(), "D00", "Queen's Pawn Opening", null, "d2d4"));
        Register(new Opening(Guid.NewGuid(), "D06", "Queen's Gambit", null, "d2d4 d7d5 c2c4"));
        Register(new Opening(Guid.NewGuid(), "A04", "Réti Opening", null, "g1f3"));
    }

    public void Register(Opening opening)
    {
        var node = _root;
        var moves = opening.StartingMovesUci.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var m in moves)
        {
            if (!node.Children.TryGetValue(m, out var next))
            {
                next = new TrieNode();
                node.Children[m] = next;
            }
            node = next;
        }
        node.Opening = opening;
    }

    public Opening? Classify(IReadOnlyList<string> uciMoves)
    {
        var node = _root;
        Opening? bestMatch = null;

        foreach (var m in uciMoves)
        {
            if (!node.Children.TryGetValue(m, out var next)) break;
            node = next;
            if (node.Opening is not null)
            {
                bestMatch = node.Opening;
            }
        }

        return bestMatch;
    }
}

public sealed class GamePhaseDetector : IGamePhaseDetector
{
    public GamePhase Detect(int ply, string? fenAfter)
    {
        // Heuristic based on ply number:
        // Opening: plies 1 - 20 (up to move 10)
        // Middlegame: plies 21 - 60
        // Endgame: plies 61+
        return ply switch
        {
            <= 20 => GamePhase.Opening,
            <= 60 => GamePhase.Middlegame,
            _ => GamePhase.Endgame
        };
    }
}

public sealed class MoveClassifier(IOptions<AccuracyOptions> options) : IMoveClassifier
{
    private readonly AccuracyOptions _options = options.Value;

    public MoveClassification Classify(Evaluation before, Evaluation after, Color mover)
    {
        int beforeCp = before.AsCentipawns();
        int afterCp = after.AsCentipawns();

        // Swing from mover's perspective:
        // White mover wants positive delta (after - before)
        // Black mover wants negative delta (before - after)
        int swing = mover == Color.White ? (beforeCp - afterCp) : (afterCp - beforeCp);

        if (swing >= _options.BlunderThresholdCentipawns) return MoveClassification.Blunder;
        if (swing >= _options.MistakeThresholdCentipawns) return MoveClassification.Mistake;
        if (swing >= _options.InaccuracyThresholdCentipawns) return MoveClassification.Inaccuracy;
        if (swing <= 10) return MoveClassification.Best;
        return MoveClassification.Good;
    }
}
