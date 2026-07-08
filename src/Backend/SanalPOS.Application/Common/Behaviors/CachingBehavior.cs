using MediatR;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;

namespace SanalPOS.Application.Common.Behaviors;

/// <summary>ICacheableQuery işaretli Query'ler için cache-aside deseni (bkz. docs/06-cache-redis.md §4).</summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan Expiry { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery cacheable)
            return await next();

        var cached = await _cache.GetAsync<TResponse>(cacheable.CacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit: {CacheKey}", cacheable.CacheKey);
            return cached;
        }

        var response = await next();
        if (response is not null)
            await _cache.SetAsync(cacheable.CacheKey, response, cacheable.Expiry, ct);
        return response;
    }
}
