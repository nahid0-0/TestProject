using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Application.Contracts.External;

public interface ILichessClient
{
    IAsyncEnumerable<RawGame> StreamGamesAsync(string username, GameQuery query, CancellationToken ct);
    Task<LichessProfile?> GetProfileAsync(string username, CancellationToken ct);
    Task<IReadOnlyList<RatingPoint>> GetRatingHistoryAsync(string username, CancellationToken ct);
}

public interface IGameParser
{
    ParsedGame Parse(RawGame raw);
}

public interface IOpeningClassifier
{
    // Longest-prefix match of the game's UCI moves against the ECO table.
    Opening? Classify(IReadOnlyList<string> uciMoves);
}

public interface IGamePhaseDetector
{
    GamePhase Detect(int ply, string? fenAfter);   // material + queens + move number heuristics
}

public interface IMoveClassifier
{
    // Uses eval swing from the mover's perspective.
    MoveClassification Classify(Evaluation before, Evaluation after, Color mover);
}
