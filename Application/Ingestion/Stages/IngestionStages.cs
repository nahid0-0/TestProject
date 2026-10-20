using Chess.Application.Contracts.External;
using Chess.Application.Contracts.Persistence;
using Chess.Application.Ingestion.Contracts;
using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Chess.Domain.ValueObjects;

namespace Chess.Application.Ingestion.Stages;

public sealed class DeduplicateStage(IGameRepository games) : IIngestionStage
{
    public int Order => 10;
    public string Name => "Deduplicate";

    public async Task ExecuteAsync(IngestionContext ctx, CancellationToken ct)
    {
        var ids = ctx.Raw.Select(r => r.Id).Distinct();
        var existing = await games.ExistingIdsAsync(ids, ct);
        ctx.Raw.RemoveAll(r => existing.Contains(r.Id));
    }
}

public sealed class ParseStage(IGameParser parser) : IIngestionStage
{
    public int Order => 20;
    public string Name => "Parse";

    public Task ExecuteAsync(IngestionContext ctx, CancellationToken ct)
    {
        foreach (var raw in ctx.Raw)
        {
            try
            {
                ctx.Parsed.Add(parser.Parse(raw));
            }
            catch (Exception ex)
            {
                ctx.Errors.Add(new IngestionError(raw.Id, Name, ex.Message));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class ValidateStage : IIngestionStage
{
    public int Order => 30;
    public string Name => "Validate";

    public Task ExecuteAsync(IngestionContext ctx, CancellationToken ct)
    {
        for (int i = ctx.Parsed.Count - 1; i >= 0; i--)
        {
            var p = ctx.Parsed[i];
            if (string.IsNullOrWhiteSpace(p.LichessGameId) ||
                string.IsNullOrWhiteSpace(p.WhiteUsername) ||
                string.IsNullOrWhiteSpace(p.BlackUsername))
            {
                ctx.Errors.Add(new IngestionError(p.LichessGameId, Name, "Missing required player or game identifier"));
                ctx.Parsed.RemoveAt(i);
                continue;
            }

            // Invariant check: contiguous plies
            bool pliesValid = true;
            for (int plyIdx = 0; plyIdx < p.Moves.Count; plyIdx++)
            {
                if (p.Moves[plyIdx].Ply != plyIdx + 1)
                {
                    pliesValid = false;
                    break;
                }
            }

            if (!pliesValid)
            {
                ctx.Errors.Add(new IngestionError(p.LichessGameId, Name, "Non-contiguous moves ply sequence"));
                ctx.Parsed.RemoveAt(i);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class EnrichStage(
    IOpeningClassifier openingClassifier,
    IGamePhaseDetector phaseDetector,
    IMoveClassifier moveClassifier) : IIngestionStage
{
    public int Order => 40;
    public string Name => "Enrich";

    public Task ExecuteAsync(IngestionContext ctx, CancellationToken ct)
    {
        foreach (var game in ctx.Parsed)
        {
            var uciMoves = game.Moves.Select(m => m.Uci).ToList();
            var opening = openingClassifier.Classify(uciMoves);

            Evaluation? previousEval = null;

            for (int i = 0; i < game.Moves.Count; i++)
            {
                var move = game.Moves[i];
                move.Phase = phaseDetector.Detect(move.Ply, move.FenAfter);

                if (previousEval is not null && move.Evaluation is not null)
                {
                    move.Classification = moveClassifier.Classify(previousEval, move.Evaluation, move.Mover);
                }

                if (move.Evaluation is not null)
                {
                    previousEval = move.Evaluation;
                }
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class ResolvePlayersStage(IPlayerRepository players, IOpeningClassifier openingClassifier) : IIngestionStage
{
    public int Order => 50;
    public string Name => "ResolvePlayers";

    public async Task ExecuteAsync(IngestionContext ctx, CancellationToken ct)
    {
        foreach (var p in ctx.Parsed)
        {
            try
            {
                var whiteUsername = p.WhiteUsername.Trim().ToLowerInvariant();
                var blackUsername = p.BlackUsername.Trim().ToLowerInvariant();

                Guid whiteId;
                Guid blackId;

                if (whiteUsername == ctx.Player.LichessUsername)
                {
                    whiteId = ctx.Player.Id;
                    var blackOpponent = await players.GetOrCreateOpponentAsync(blackUsername, ct);
                    blackId = blackOpponent.Id;
                }
                else if (blackUsername == ctx.Player.LichessUsername)
                {
                    blackId = ctx.Player.Id;
                    var whiteOpponent = await players.GetOrCreateOpponentAsync(whiteUsername, ct);
                    whiteId = whiteOpponent.Id;
                }
                else
                {
                    var whiteOpponent = await players.GetOrCreateOpponentAsync(whiteUsername, ct);
                    var blackOpponent = await players.GetOrCreateOpponentAsync(blackUsername, ct);
                    whiteId = whiteOpponent.Id;
                    blackId = blackOpponent.Id;
                }

                var opening = openingClassifier.Classify(p.Moves.Select(m => m.Uci).ToList());

                ctx.Drafts.Add(new GameDraft(
                    LichessGameId: p.LichessGameId,
                    WhitePlayerId: whiteId,
                    BlackPlayerId: blackId,
                    Result: p.Result,
                    Termination: p.Termination,
                    TimeControl: p.TimeControl,
                    Rated: p.Rated,
                    PlayedAt: p.PlayedAt,
                    Moves: p.Moves,
                    WhiteRating: p.WhiteRating,
                    BlackRating: p.BlackRating,
                    WhiteRatingDiff: p.WhiteRatingDiff,
                    BlackRatingDiff: p.BlackRatingDiff,
                    OpeningId: opening?.Id,
                    Variant: p.Variant));
            }
            catch (Exception ex)
            {
                ctx.Errors.Add(new IngestionError(p.LichessGameId, Name, ex.Message));
            }
        }
    }
}

public sealed class BuildEntitiesStage : IIngestionStage
{
    public int Order => 60;
    public string Name => "BuildEntities";

    public Task ExecuteAsync(IngestionContext ctx, CancellationToken ct)
    {
        foreach (var draft in ctx.Drafts)
        {
            try
            {
                var game = Game.Create(draft);
                ctx.ToPersist.Add(game);
            }
            catch (Exception ex)
            {
                ctx.Errors.Add(new IngestionError(draft.LichessGameId, Name, ex.Message));
            }
        }
        return Task.CompletedTask;
    }
}
