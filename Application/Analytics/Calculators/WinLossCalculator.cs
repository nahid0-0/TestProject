using Chess.Application.Analytics.Contracts;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chess.Application.Analytics.Calculators;

public sealed record WinLossResult : AnalyticsResult
{
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
    public double ScorePercent { get; init; }
    public IReadOnlyList<WinLossBucket> ByColor { get; init; } = [];
    public IReadOnlyList<WinLossBucket> ByOpponentBand { get; init; } = [];
}

public sealed record WinLossBucket(string Label, int Wins, int Draws, int Losses);

public sealed class WinLossCalculator(IGameRepository games) : AnalyticsCalculator<WinLossResult>(games)
{
    public override string Key => "win-loss";
    public override EnabledModules Module => EnabledModules.WinLoss;

    protected override async Task<WinLossResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        var rows = await q.Select(x => new { x.Color, x.Score, x.OpponentRating, x.OwnRating }).ToListAsync(ct);

        static WinLossBucket Bucket(string label, IEnumerable<double> scores)
        {
            var s = scores.ToList();
            return new(label, s.Count(x => x == 1.0), s.Count(x => x == 0.5), s.Count(x => x == 0.0));
        }

        var all = Bucket("all", rows.Select(r => r.Score));
        return new WinLossResult
        {
            Wins = all.Wins,
            Draws = all.Draws,
            Losses = all.Losses,
            ScorePercent = rows.Count == 0 ? 0 : rows.Average(r => r.Score) * 100,
            ByColor = [
                Bucket("white", rows.Where(r => r.Color == Color.White).Select(r => r.Score)),
                Bucket("black", rows.Where(r => r.Color == Color.Black).Select(r => r.Score))
            ],
            ByOpponentBand = [
                Bucket("higher", rows.Where(r => r.OpponentRating > r.OwnRating + 50).Select(r => r.Score)),
                Bucket("equal",  rows.Where(r => Math.Abs((r.OpponentRating ?? 0) - (r.OwnRating ?? 0)) <= 50).Select(r => r.Score)),
                Bucket("lower",  rows.Where(r => r.OpponentRating < r.OwnRating - 50).Select(r => r.Score))
            ]
        };
    }
}
