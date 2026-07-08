using SanalPOS.Application.Common.Interfaces;
using StackExchange.Redis;

namespace SanalPOS.Infrastructure.Redis;

/// <summary>
/// SET NX PX tabanlı basit distributed lock. Lock değeri rastgele token'dır;
/// bırakırken token doğrulanır ki başka instance'ın lock'u yanlışlıkla silinmesin.
/// </summary>
public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisDistributedLockService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var acquired = await db.StringSetAsync(key, token, expiry, When.NotExists);
        return acquired ? new RedisLockHandle(db, key, token) : null;
    }

    private sealed class RedisLockHandle : IAsyncDisposable
    {
        private const string ReleaseScript = """
            if redis.call('GET', KEYS[1]) == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            else
                return 0
            end
            """;

        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _token;

        public RedisLockHandle(IDatabase db, string key, string token)
        {
            _db = db;
            _key = key;
            _token = token;
        }

        public async ValueTask DisposeAsync() =>
            await _db.ScriptEvaluateAsync(ReleaseScript, [(RedisKey)_key], [(RedisValue)_token]);
    }
}
