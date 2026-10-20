using Chess.Application.Analytics.Contracts;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chess.Application.Analytics;

public abstract class AnalyticsCalculator<TResult>(IGameRepository games) : IAnalyticsCalculator
    where TResult : AnalyticsResult
{
    public abstract string Key { get; }
    public abstract EnabledModules Module { get; }
    protected virtual int MinimumSample => 30;
    protected virtual bool RequiresEvaluations => false;

    public Type ResultType => typeof(TResult);

    public async Task<AnalyticsResult> RunAsync(AnalyticsContext ctx, CancellationToken ct)
    {
        var query = BuildQuery(ctx);
        var sample = await query.CountAsync(ct);

        double? coverage = null;
        if (RequiresEvaluations)
        {
            var withEval = await query.CountAsync(g => g.HasEvaluations, ct);
            coverage = sample == 0 ? 0 : (double)withEval / sample;
            query = query.Where(g => g.HasEvaluations);
            sample = withEval;
        }

        var result = await ComputeAsync(ctx, query, ct);
        return result with
        {
            SampleSize = sample,
            IsSufficient = sample >= MinimumSample,
            EvalCoverage = coverage
        };
    }

    protected abstract Task<TResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> games, CancellationToken ct);

    protected IQueryable<PlayerGame> BuildQuery(AnalyticsContext ctx)
    {
        var pid = ctx.PlayerId;
        var f = ctx.Filter;

        var q = games.Query().Where(g => g.WhitePlayerId == pid || g.BlackPlayerId == pid);
        if (f.From is { } from) q = q.Where(g => g.PlayedAt >= from);
        if (f.To is { } to) q = q.Where(g => g.PlayedAt <= to);
        if (f.RatedOnly == true) q = q.Where(g => g.Rated);
        if (f.Variant is { } v) q = q.Where(g => g.Variant == v);

        if (f.Category is { } cat)
        {
            var (minSec, maxSec) = cat switch
            {
                TimeControlCategory.UltraBullet => (0, 29),
                TimeControlCategory.Bullet => (30, 179),
                TimeControlCategory.Blitz => (180, 479),
                TimeControlCategory.Rapid => (480, 1499),
                TimeControlCategory.Classical => (1500, int.MaxValue),
                _ => (0, int.MaxValue)
            };
            q = q.Where(g => (g.TimeControl.InitialSeconds + 40 * g.TimeControl.IncrementSeconds) >= minSec &&
                             (g.TimeControl.InitialSeconds + 40 * g.TimeControl.IncrementSeconds) <= maxSec);
        }

        var view = q.Select(g => new PlayerGame(
            g.Id,
            g.PlayedAt,
            g.WhitePlayerId == pid ? Color.White : Color.Black,
            g.Result == GameResult.Draw ? 0.5
              : g.Result == GameResult.WhiteWin ? (g.WhitePlayerId == pid ? 1.0 : 0.0)
              : g.Result == GameResult.BlackWin ? (g.BlackPlayerId == pid ? 1.0 : 0.0) : 0.5,
            g.WhitePlayerId == pid ? g.WhiteRating : g.BlackRating,
            g.WhitePlayerId == pid ? g.BlackRating : g.WhiteRating,
            g.WhitePlayerId == pid ? g.WhiteRatingDiff : g.BlackRatingDiff,
            g.TimeControl,
            g.Termination,
            g.OpeningId,
            g.HasEvaluations));

        if (f.Color is { } c) view = view.Where(x => x.Color == c);
        if (f.OpponentRatingMin is { } min) view = view.Where(x => x.OpponentRating >= min);
        if (f.OpponentRatingMax is { } max) view = view.Where(x => x.OpponentRating <= max);
        return view;
    }
}
