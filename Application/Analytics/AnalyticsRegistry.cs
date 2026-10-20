using Chess.Application.Analytics.Contracts;
using Chess.Application.Common;

namespace Chess.Application.Analytics;

public sealed class AnalyticsRegistry(IEnumerable<IAnalyticsCalculator> calculators) : IAnalyticsRegistry
{
    private readonly Dictionary<string, IAnalyticsCalculator> _map =
        calculators.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Keys => _map.Keys;

    public IAnalyticsCalculator Get(string key) =>
        _map.TryGetValue(key, out var c) ? c : throw new NotFoundException($"Unknown module '{key}'");
}
