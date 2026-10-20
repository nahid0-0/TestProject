using Chess.Application.Analytics.Calculators;
using Chess.Application.Analytics.Contracts;
using Chess.Application.Comparison.Contracts;

namespace Chess.Application.Comparison;

public sealed class ComparisonService(
    IAnalyticsRegistry registry,
    ICohortBaselineProvider cohorts,
    IEnumerable<IMetricExtractor> extractors,
    ISignificanceTest significance)
{
    public async Task<ComparisonResult> CompareAsync(ComparisonRequest req, CancellationToken ct)
    {
        var calc = registry.Get(req.ModuleKey);
        var a = await calc.RunAsync(new AnalyticsContext(req.PlayerA, req.Filter), ct);
        var b = req.PlayerB is { } pb
            ? await calc.RunAsync(new AnalyticsContext(pb, req.Filter), ct)
            : await cohorts.GetBaselineAsync(req.ModuleKey, req.Cohort!, ct);

        var ex = extractors.FirstOrDefault(e => e.ResultType == calc.ResultType)
            ?? throw new InvalidOperationException($"No metric extractor found for result type {calc.ResultType.Name}");

        var ma = ex.Extract(a);
        var mb = ex.Extract(b);

        var deltas = ma.Keys.Where(mb.ContainsKey).Select(k => new MetricDelta(
            k,
            ma[k],
            mb[k],
            ma[k] - mb[k],
            significance.IsSignificant(ma[k], a.SampleSize, mb[k], b.SampleSize))).ToList();

        return new ComparisonResult(req.ModuleKey, a, b, deltas);
    }
}

public sealed class SignificanceTest : ISignificanceTest
{
    // Two-proportion z-test with critical value for alpha = 0.05 (|z| > 1.96)
    public bool IsSignificant(double a, int nA, double b, int nB)
    {
        if (nA < 10 || nB < 10) return false;

        // Convert percentage to proportion if necessary
        double p1 = a > 1.0 ? a / 100.0 : a;
        double p2 = b > 1.0 ? b / 100.0 : b;

        p1 = Math.Clamp(p1, 0.0, 1.0);
        p2 = Math.Clamp(p2, 0.0, 1.0);

        double pPool = (p1 * nA + p2 * nB) / (nA + nB);
        double se = Math.Sqrt(pPool * (1 - pPool) * (1.0 / nA + 1.0 / nB));

        if (se <= 0.000001) return false;
        double z = Math.Abs(p1 - p2) / se;
        return z >= 1.96;
    }
}

public sealed class WinLossMetricExtractor : IMetricExtractor
{
    public Type ResultType => typeof(WinLossResult);

    public IReadOnlyDictionary<string, double> Extract(AnalyticsResult r)
    {
        if (r is not WinLossResult wl) return new Dictionary<string, double>();

        return new Dictionary<string, double>
        {
            ["ScorePercent"] = wl.ScorePercent,
            ["Wins"] = wl.Wins,
            ["Draws"] = wl.Draws,
            ["Losses"] = wl.Losses
        };
    }
}

public sealed class CohortBaselineProvider(IAnalyticsRegistry registry) : ICohortBaselineProvider
{
    public Task<AnalyticsResult> GetBaselineAsync(string moduleKey, CohortSpec cohort, CancellationToken ct)
    {
        // Default baseline implementation: run module calculator with a dummy/aggregate filter
        var calc = registry.Get(moduleKey);
        var filter = new AnalyticsFilter
        {
            Category = cohort.Category,
            OpponentRatingMin = cohort.RatingMin,
            OpponentRatingMax = cohort.RatingMax
        };

        // When cohort is requested without a specific player, return placeholder baseline
        if (calc.ResultType == typeof(WinLossResult))
        {
            return Task.FromResult<AnalyticsResult>(new WinLossResult
            {
                Wins = 50,
                Draws = 15,
                Losses = 35,
                ScorePercent = 57.5,
                SampleSize = 100,
                IsSufficient = true
            });
        }

        return Task.FromResult<AnalyticsResult>(new AccuracyResult
        {
            AverageCentipawnLoss = 40.0,
            BlunderRatePercent = 5.0,
            MistakeRatePercent = 9.0,
            InaccuracyRatePercent = 14.0,
            SampleSize = 100,
            IsSufficient = true
        });
    }
}
