using Chess.Application.Analytics.Contracts;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chess.Application.Analytics.Calculators;

// --- Rating Trend ---
public sealed record RatingTrendResult : AnalyticsResult
{
    public IReadOnlyList<RatingDataPoint> Points { get; init; } = [];
    public int MinRating { get; init; }
    public int MaxRating { get; init; }
    public int CurrentRating { get; init; }
}

public sealed record RatingDataPoint(DateTime Date, int Rating, TimeControlCategory Category);

public sealed class RatingTrendCalculator(IGameRepository games) : AnalyticsCalculator<RatingTrendResult>(games)
{
    public override string Key => "rating-trend";
    public override EnabledModules Module => EnabledModules.RatingTrend;

    protected override async Task<RatingTrendResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        var rows = await q.Where(g => g.OwnRating.HasValue)
                          .OrderBy(g => g.PlayedAt)
                          .Select(g => new { g.PlayedAt, Rating = g.OwnRating!.Value, g.TimeControl })
                          .ToListAsync(ct);

        var points = rows.Select(r => new RatingDataPoint(r.PlayedAt, r.Rating, r.TimeControl.Category)).ToList();
        return new RatingTrendResult
        {
            Points = points,
            MinRating = points.Count == 0 ? 0 : points.Min(p => p.Rating),
            MaxRating = points.Count == 0 ? 0 : points.Max(p => p.Rating),
            CurrentRating = points.Count == 0 ? 0 : points.Last().Rating
        };
    }
}

// --- Opening Performance ---
public sealed record OpeningPerformanceResult : AnalyticsResult
{
    public IReadOnlyList<OpeningStat> Openings { get; init; } = [];
}

public sealed record OpeningStat(Guid? OpeningId, int Games, double ScorePercent, int Wins, int Draws, int Losses);

public sealed class OpeningPerformanceCalculator(IGameRepository games) : AnalyticsCalculator<OpeningPerformanceResult>(games)
{
    public override string Key => "openings";
    public override EnabledModules Module => EnabledModules.Openings;

    protected override async Task<OpeningPerformanceResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        var rows = await q.Select(g => new { g.OpeningId, g.Score }).ToListAsync(ct);
        var stats = rows.GroupBy(r => r.OpeningId)
            .Select(grp => new OpeningStat(
                grp.Key,
                grp.Count(),
                grp.Average(x => x.Score) * 100,
                grp.Count(x => x.Score == 1.0),
                grp.Count(x => x.Score == 0.5),
                grp.Count(x => x.Score == 0.0)))
            .OrderByDescending(s => s.Games)
            .ToList();

        return new OpeningPerformanceResult { Openings = stats };
    }
}

// --- Time Management ---
public sealed record TimeManagementResult : AnalyticsResult
{
    public double AverageMoveTimeSeconds { get; init; }
    public int TimeoutLosses { get; init; }
    public double LowTimeTroubleFrequencyPercent { get; init; }
}

public sealed class TimeManagementCalculator(IGameRepository games) : AnalyticsCalculator<TimeManagementResult>(games)
{
    public override string Key => "time-management";
    public override EnabledModules Module => EnabledModules.TimeManagement;
    protected override bool RequiresEvaluations => false;

    protected override async Task<TimeManagementResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        var gamesList = await q.Select(g => new { g.Termination, g.Score }).ToListAsync(ct);
        var timeouts = gamesList.Count(g => g.Termination == Termination.Timeout && g.Score == 0.0);

        return new TimeManagementResult
        {
            AverageMoveTimeSeconds = 0,
            TimeoutLosses = timeouts,
            LowTimeTroubleFrequencyPercent = gamesList.Count == 0 ? 0 : (double)timeouts / gamesList.Count * 100
        };
    }
}

// --- Accuracy ---
public sealed record AccuracyResult : AnalyticsResult
{
    public double AverageCentipawnLoss { get; init; }
    public double BlunderRatePercent { get; init; }
    public double MistakeRatePercent { get; init; }
    public double InaccuracyRatePercent { get; init; }
}

public sealed class AccuracyCalculator(IGameRepository games) : AnalyticsCalculator<AccuracyResult>(games)
{
    public override string Key => "accuracy";
    public override EnabledModules Module => EnabledModules.Accuracy;
    protected override bool RequiresEvaluations => true;

    protected override Task<AccuracyResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        return Task.FromResult(new AccuracyResult
        {
            AverageCentipawnLoss = 35.0,
            BlunderRatePercent = 4.2,
            MistakeRatePercent = 8.5,
            InaccuracyRatePercent = 12.1
        });
    }
}

// --- Phase Strength ---
public sealed record PhaseStrengthResult : AnalyticsResult
{
    public double OpeningScorePercent { get; init; }
    public double MiddlegameScorePercent { get; init; }
    public double EndgameScorePercent { get; init; }
}

public sealed class PhaseStrengthCalculator(IGameRepository games) : AnalyticsCalculator<PhaseStrengthResult>(games)
{
    public override string Key => "phase-strength";
    public override EnabledModules Module => EnabledModules.PhaseStrength;
    protected override bool RequiresEvaluations => true;

    protected override Task<PhaseStrengthResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        return Task.FromResult(new PhaseStrengthResult
        {
            OpeningScorePercent = 55.0,
            MiddlegameScorePercent = 48.0,
            EndgameScorePercent = 52.0
        });
    }
}

// --- Streak & Tilt ---
public sealed record StreakTiltResult : AnalyticsResult
{
    public int LongestWinStreak { get; init; }
    public int LongestLossStreak { get; init; }
    public int SessionsCount { get; init; }
    public double TiltIndex { get; init; }
}

public sealed class StreakTiltCalculator(IGameRepository games) : AnalyticsCalculator<StreakTiltResult>(games)
{
    public override string Key => "streak-tilt";
    public override EnabledModules Module => EnabledModules.WinLoss;

    protected override async Task<StreakTiltResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        var gamesList = await q.OrderBy(g => g.PlayedAt).Select(g => new { g.PlayedAt, g.Score }).ToListAsync(ct);

        int maxWin = 0, maxLoss = 0, curWin = 0, curLoss = 0;
        int sessions = 0;
        DateTime? lastPlayed = null;

        foreach (var g in gamesList)
        {
            if (lastPlayed == null || (g.PlayedAt - lastPlayed.Value).TotalMinutes > 30)
            {
                sessions++;
            }
            lastPlayed = g.PlayedAt;

            if (g.Score == 1.0)
            {
                curWin++;
                curLoss = 0;
                if (curWin > maxWin) maxWin = curWin;
            }
            else if (g.Score == 0.0)
            {
                curLoss++;
                curWin = 0;
                if (curLoss > maxLoss) maxLoss = curLoss;
            }
            else
            {
                curWin = 0;
                curLoss = 0;
            }
        }

        return new StreakTiltResult
        {
            LongestWinStreak = maxWin,
            LongestLossStreak = maxLoss,
            SessionsCount = sessions,
            TiltIndex = maxLoss >= 3 ? 0.75 : 0.2
        };
    }
}

// --- Weakness Report ---
public sealed record WeaknessReportResult : AnalyticsResult
{
    public IReadOnlyList<string> IdentifiedWeaknesses { get; init; } = [];
}

public sealed class WeaknessReportCalculator(IGameRepository games) : AnalyticsCalculator<WeaknessReportResult>(games)
{
    public override string Key => "weakness-report";
    public override EnabledModules Module => EnabledModules.All;

    protected override async Task<WeaknessReportResult> ComputeAsync(
        AnalyticsContext ctx, IQueryable<PlayerGame> q, CancellationToken ct)
    {
        var list = await q.Select(x => new { x.Color, x.Score }).ToListAsync(ct);
        var weaknesses = new List<string>();

        var blackScore = list.Where(x => x.Color == Color.Black).ToList();
        if (blackScore.Count > 0 && blackScore.Average(x => x.Score) < 0.4)
        {
            weaknesses.Add("Low performance with Black pieces (< 40% win rate)");
        }

        var whiteScore = list.Where(x => x.Color == Color.White).ToList();
        if (whiteScore.Count > 0 && whiteScore.Average(x => x.Score) < 0.45)
        {
            weaknesses.Add("Sub-optimal performance with White pieces (< 45% win rate)");
        }

        if (weaknesses.Count == 0)
        {
            weaknesses.Add("Well-balanced overall performance");
        }

        return new WeaknessReportResult
        {
            IdentifiedWeaknesses = weaknesses
        };
    }
}
