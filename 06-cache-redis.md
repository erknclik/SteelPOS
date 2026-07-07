# SanalPOS - Cache Stratejisi (Redis)

## 1. Kullanım Amaçları

| Amaç | Açıklama |
|---|---|
| Query Cache | Sık okunan, az değişen veriler (merchant bilgisi, komisyon kuralları, banka provider listesi) |
| Idempotency Store | `Idempotency-Key` bazlı tekrar eden istek kontrolü (kısa TTL) |
| Rate Limiting | Merchant/IP bazlı istek sayacı (sliding window) |
| Distributed Lock | Aynı anda aynı işlem üzerinde çakışmayı önleme (`RedLock.net`) |
| Session/Token Blacklist | Çıkış yapılan/iptal edilen JWT'lerin kısa süreli kara listesi |
| SignalR Backplane (opsiyonel) | Çoklu instance'da gerçek zamanlı bildirim senkronizasyonu |

## 2. Mimari Entegrasyon

`ICacheService` arayüzü Application katmanında tanımlanır, Infrastructure katmanında `StackExchange.Redis` ile implemente edilir:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default);
}
```

```csharp
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, JsonSerializer.Serialize(value), expiry);
    }
    // ...
}
```

Kayıt (DI):
```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// ASP.NET Core distributed cache (session vb. için)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "SanalPOS:";
});
```

## 3. Key Tasarım Kuralları

Format: `{servis}:{modül}:{entity}:{id}:{alan}`

| Örnek Key | Açıklama | TTL |
|---|---|---|
| `sanalpos:merchant:{merchantId}` | Merchant detay cache'i | 15 dk |
| `sanalpos:commission-rules:{merchantId}` | Komisyon kuralları | 30 dk |
| `sanalpos:idempotency:{idempotencyKey}` | İdempotency kaydı | 24 saat |
| `sanalpos:ratelimit:{merchantId}:{yyyyMMddHHmm}` | Dakikalık istek sayacı | 2 dk |
| `sanalpos:lock:tx:{transactionId}` | İşlem bazlı distributed lock | 30 sn |
| `sanalpos:jwt-blacklist:{jti}` | İptal edilmiş token | Token'ın kalan ömrü kadar |

## 4. Cache-Aside Deseni (MediatR Pipeline Behavior)

Query'ler için opsiyonel bir `CachingBehavior<TRequest, TResponse>` pipeline'a eklenir; `ICacheableQuery` işaretleyici arayüzünü uygulayan Query'ler otomatik cache'lenir:

```csharp
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan Expiry { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery cacheable) return await next();

        var cached = await _cache.GetAsync<TResponse>(cacheable.CacheKey, ct);
        if (cached is not null) return cached;

        var response = await next();
        await _cache.SetAsync(cacheable.CacheKey, response, cacheable.Expiry, ct);
        return response;
    }
}
```

## 5. Cache Invalidation

- **Command sonrası aktif invalidation**: Bir entity güncellendiğinde ilgili key(ler) `RemoveAsync` ile silinir (ör. `UpdateMerchantCommandHandler`, işlem sonunda `sanalpos:merchant:{id}` key'ini invalidate eder)
- **TTL bazlı pasif invalidation**: Her key için makul bir TTL belirlenir, hiçbir key TTL'siz (kalıcı) yazılmaz (idempotency ve blacklist hariç, onlarda da üst sınır TTL vardır)
- **Pattern bazlı silme** gerektiğinde `SCAN` komutu kullanılır (`KEYS` komutu prod ortamda **kullanılmaz**, blocking olduğu için performans riski taşır)

## 6. Idempotency Uygulaması (Ödeme İşlemlerinde Kritik)

```csharp
var acquired = await _cache.SetIfNotExistsAsync(
    $"sanalpos:idempotency:{request.IdempotencyKey}",
    transactionId.ToString(),
    TimeSpan.FromHours(24));

if (!acquired)
{
    var existingTransactionId = await _cache.GetAsync<string>($"sanalpos:idempotency:{request.IdempotencyKey}");
    // Var olan işlemi döndür, yeni işlem oluşturma
}
```

## 7. Yüksek Erişilebilirlik

- Prod ortamında **Redis Sentinel** veya **Redis Cluster** kullanılması önerilir (tek node single-point-of-failure oluşturur)
- Bağlantı dizesinde birden fazla endpoint tanımlanır, `abortConnect=false` ayarı ile geçici kopmalarda uygulamanın çökmesi engellenir

## 8. İzleme

- `AspNetCore.HealthChecks.Redis` ile `/health` endpoint'ine Redis kontrolü eklenir
- Redis `INFO` metrikleri (hit/miss oranı, bellek kullanımı, bağlı client sayısı) Prometheus `redis_exporter` ile Grafana'ya taşınır
