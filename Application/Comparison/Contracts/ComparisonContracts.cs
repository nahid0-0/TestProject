using Chess.Application.Analytics.Contracts;
using Chess.Domain.Enums;

namespace Chess.Application.Comparison.Contracts;

public sealed record ComparisonRequest(
    string ModuleKey,
    Guid PlayerA,
    Guid? PlayerB,
    CohortSpec? Cohort,
    AnalyticsFilter Filter);

public sealed record CohortSpec(int RatingMin, int RatingMax, TimeControlCategory Category);

public sealed record ComparisonResult(
    string ModuleKey,
    AnalyticsResult A,
    AnalyticsResult Baseline,
    IReadOnlyList<MetricDelta> Deltas);

public sealed record MetricDelta(string Metric, double A, double B, double Difference, bool Significant);

public interface IMetricExtractor      // one per result type: turns a result into named numbers
{
    Type ResultType { get; }
    IReadOnlyDictionary<string, double> Extract(AnalyticsResult r);
}

public interface ICohortBaselineProvider
{
    Task<AnalyticsResult> GetBaselineAsync(string moduleKey, CohortSpec cohort, CancellationToken ct);
}

public interface ISignificanceTest
{
    bool IsSignificant(double a, int nA, double b, int nB);   // two-proportion z-test for rates
}
