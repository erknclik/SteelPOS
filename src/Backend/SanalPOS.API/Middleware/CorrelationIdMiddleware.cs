using SanalPOS.Infrastructure.Services;

namespace SanalPOS.API.Middleware;

/// <summary>
/// Her istekte X-Correlation-Id header'ını okur (yoksa üretir), HttpContext.Items'a ve
/// response header'ına yazar; log scope'una ekler (uçtan uca izlenebilirlik).
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdAccessor.HeaderName, out var value)
                            && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        context.Items[CorrelationIdAccessor.ItemKey] = correlationId;
        context.Response.Headers[CorrelationIdAccessor.HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
