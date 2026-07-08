namespace SanalPOS.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default);
}

public interface IDistributedLockService
{
    /// <summary>Lock alınamazsa null döner; alınırsa dispose edildiğinde lock bırakılır.</summary>
    Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}

/// <summary>Redis key isimlendirme standardı (bkz. docs/06-cache-redis.md §3).</summary>
public static class CacheKeys
{
    public static string Merchant(Guid merchantId) => $"sanalpos:merchant:{merchantId}";
    public static string CommissionRules(Guid merchantId) => $"sanalpos:commission-rules:{merchantId}";
    public static string Idempotency(string idempotencyKey) => $"sanalpos:idempotency:{idempotencyKey}";
    public static string TransactionLock(Guid transactionId) => $"sanalpos:lock:tx:{transactionId}";
    public static string JwtBlacklist(string jti) => $"sanalpos:jwt-blacklist:{jti}";
}
