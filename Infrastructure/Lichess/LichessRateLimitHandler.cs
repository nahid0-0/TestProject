using System.Net;

namespace Chess.Infrastructure.Lichess;

public sealed class LichessRateLimitHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _blockedUntil = DateTime.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            if (_blockedUntil > now)
            {
                var delay = _blockedUntil - now;
                await Task.Delay(delay, cancellationToken);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
                _blockedUntil = DateTime.UtcNow.Add(retryAfter);
            }

            return response;
        }
        finally
        {
            Gate.Release();
        }
    }
}
