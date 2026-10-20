using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Chess.Api.Middleware;

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> log)
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationHeader, out var correlationId) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Response.Headers[CorrelationHeader] = correlationId;

        using (log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId.ToString() }))
        {
            await next(context);
        }
    }
}
