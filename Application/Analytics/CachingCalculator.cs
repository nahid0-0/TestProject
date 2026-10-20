using Chess.Application.Analytics.Contracts;
using Chess.Application.Contracts.Persistence;
using Chess.Domain.Enums;

namespace Chess.Application.Analytics;

public sealed class CachingCalculator(IAnalyticsCalculator inner, IAnalyticsSnapshotStore store) : IAnalyticsCalculator
{
    public string Key => inner.Key;
    public Type ResultType => inner.ResultType;
    public EnabledModules Module => inner.Module;

    public async Task<AnalyticsResult> RunAsync(AnalyticsContext ctx, CancellationToken ct)
    {
        var cached = await store.TryGetAsync(ctx.PlayerId, Key, ctx.Filter.Hash(), ResultType, ct);
        if (cached is AnalyticsResult result)
        {
            return result;
        }

        var fresh = await inner.RunAsync(ctx, ct);
        await store.SaveAsync(ctx.PlayerId, Key, ctx.Filter.Hash(), fresh, ct);
        return fresh;
    }
}
