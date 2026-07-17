using SanalPOS.Infrastructure.Iso8583.Network;
using StackExchange.Redis;

namespace SanalPOS.Infrastructure.BankAdapters;

/// <summary>
/// Redis INCR tabanlı, banka başına merkezî STAN (DE11) sayacı. Çok instance'lı
/// üretimde tüm API/worker süreçleri aynı sayaçtan çektiği için STAN çakışması olmaz.
/// Sayaç kalıcıdır (AOF/RDB); 999999 sonrası 1'e döner (ISO 8583 döngü kuralı).
/// </summary>
public sealed class RedisStanSequence : IStanSequence
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _key;

    public RedisStanSequence(IConnectionMultiplexer redis, string providerCode)
    {
        _redis = redis;
        _key = $"sanalpos:iso8583:stan:{providerCode.ToLowerInvariant()}";
    }

    public async ValueTask<string> NextAsync(CancellationToken ct = default)
    {
        var value = await _redis.GetDatabase().StringIncrementAsync(_key);
        return (((value - 1) % 999_999) + 1).ToString("D6");
    }
}

public sealed class RedisStanSequenceFactory : IStanSequenceFactory
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStanSequenceFactory(IConnectionMultiplexer redis) => _redis = redis;

    public IStanSequence Create(string providerCode) => new RedisStanSequence(_redis, providerCode);
}
