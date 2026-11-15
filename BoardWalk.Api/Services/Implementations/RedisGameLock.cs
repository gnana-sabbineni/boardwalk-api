using BoardWalk.Monopoly.Abstractions;
using StackExchange.Redis;

namespace BoardWalk.Api.Services.Implementations
{
    /// <summary>
    /// Per-game lock shared by every API server, built on Redis "SET key token NX PX ttl":
    ///  - NX: only one caller can create the key (that caller holds the lock);
    ///  - PX ttl: the key expires by itself, so a crashed server cannot leave a game locked forever;
    ///  - a random token + a compare-and-delete script on release: a slow holder whose lock already expired cannot
    ///    delete the lock that now belongs to someone else.
    /// </summary>
    public class RedisGameLock : IGameLock
    {
        private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);

        private const string ReleaseScript =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        private readonly IConnectionMultiplexer _redis;

        public RedisGameLock(IConnectionMultiplexer redis) => _redis = redis;

        public async Task<IAsyncDisposable?> TryAcquireAsync(Guid gameId, TimeSpan wait, CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            var key = (RedisKey)$"lock:game:{gameId}";
            var token = Guid.NewGuid().ToString("N");
            var deadline = DateTime.UtcNow + wait;

            while (true)
            {
                if (await db.StringSetAsync(key, token, LockTtl, When.NotExists))
                    return new Releaser(db, key, token);

                if (DateTime.UtcNow >= deadline) return null;
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private readonly IDatabase _db;
            private readonly RedisKey _key;
            private readonly string _token;

            public Releaser(IDatabase db, RedisKey key, string token)
            {
                _db = db;
                _key = key;
                _token = token;
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await _db.ScriptEvaluateAsync(ReleaseScript, new[] { _key }, new RedisValue[] { _token });
                }
                catch (RedisException)
                {
                    // Redis hiccup on release: the TTL frees the lock shortly anyway.
                }
            }
        }
    }
}
