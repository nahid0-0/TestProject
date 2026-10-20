using Chess.Application.Contracts.External;
using Chess.Application.Contracts.Persistence;
using Chess.Application.Ingestion.Contracts;
using Chess.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chess.Application.Ingestion;

public sealed class IngestionPipeline(
    ILichessClient lichess,
    IEnumerable<IIngestionStage> stages,
    IGameRepository games,
    IPlayerRepository players,
    IUnitOfWork uow,
    IOptions<IngestionOptions> options,
    ILogger<IngestionPipeline> log)
{
    private readonly IIngestionStage[] _stages = stages.OrderBy(s => s.Order).ToArray();

    public async Task RunAsync(Player player, SyncJob job, GameQuery query, CancellationToken ct)
    {
        _ = players;
        var batchSize = options.Value.BatchSize;
        int imported = 0, failed = 0;
        var ctx = NewContext(player, job);

        await foreach (var raw in lichess.StreamGamesAsync(player.LichessUsername, query, ct))
        {
            var timestamp = raw.LastMoveAt ?? raw.CreatedAt ?? 0;
            if (timestamp > ctx.MaxTimestampMs)
            {
                ctx.MaxTimestampMs = timestamp;
            }

            ctx.Raw.Add(raw);
            if (ctx.Raw.Count < batchSize) continue;

            (imported, failed) = await FlushAsync(ctx, imported, failed, ct);
            var maxTs = ctx.MaxTimestampMs;
            ctx = NewContext(player, job);
            ctx.MaxTimestampMs = maxTs;
        }

        if (ctx.Raw.Count > 0)
        {
            (imported, failed) = await FlushAsync(ctx, imported, failed, ct);
        }
    }

    private async Task<(int, int)> FlushAsync(IngestionContext ctx, int imported, int failed, CancellationToken ct)
    {
        log.LogInformation("Processing batch of {Count} raw games for {User}", ctx.Raw.Count, ctx.Player.LichessUsername);

        foreach (var stage in _stages)
        {
            await stage.ExecuteAsync(ctx, ct);
        }

        if (ctx.ToPersist.Count > 0)
        {
            await games.AddRangeAsync(ctx.ToPersist, ct);
        }

        if (ctx.MaxTimestampMs > 0)
        {
            ctx.Player.AdvanceCursor(ctx.MaxTimestampMs);
        }

        await uow.SaveChangesAsync(ct);   // one transaction per batch

        imported += ctx.ToPersist.Count;
        failed += ctx.Errors.Count;
        ctx.Job.ReportProgress(imported, failed);
        await uow.SaveChangesAsync(ct);
        return (imported, failed);
    }

    private static IngestionContext NewContext(Player p, SyncJob j) => new() { Player = p, Job = j };
}
