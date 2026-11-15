using StackExchange.Redis;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Services.Implementations
{
    public class RedisPresenceService : IPresenceService
    {
        private readonly IDatabase _db;

        // Lua script: atomically check-and-delete. Redis guarantees this runs as one
        // indivisible step — no other command (including a concurrent "mark online"
        // write) can execute in the middle of it.
        private const string CheckAndClearScript = @"
            local current = redis.call('GET', KEYS[1])
            if current == ARGV[1] then
                redis.call('DEL', KEYS[1])
                return 1
            else
                return 0
            end";

        public RedisPresenceService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        private static string PresenceKey(Guid userId) => $"presence:{userId}";
        private static string ConnectionCountKey(Guid userId) => $"presence:{userId}:count";

        public async Task MarkOnlineAsync(Guid userId)
        {
            await _db.StringSetAsync(PresenceKey(userId), "online");
            await _db.StringIncrementAsync(ConnectionCountKey(userId));
        }

        public async Task<int> MarkOfflineAsync(Guid userId)
        {
            var remaining = await _db.StringDecrementAsync(ConnectionCountKey(userId));

            // Only actually go "offline" once ALL of this user's connections are gone —
            // handles the multiple-tabs case (design doc §7.5).
            if (remaining <= 0)
            {
                await _db.StringSetAsync(PresenceKey(userId), "offline");
                await _db.KeyDeleteAsync(ConnectionCountKey(userId));
                return 0;
            }

            return (int)remaining;
        }

        public async Task<bool> IsOnlineAsync(Guid userId)
        {
            var value = await _db.StringGetAsync(PresenceKey(userId));
            return value == "online";
        }

        public async Task<bool> ConfirmStillOfflineAndClearAsync(Guid userId)
        {
            var result = await _db.ScriptEvaluateAsync(
                CheckAndClearScript,
                new RedisKey[] { PresenceKey(userId) },
                new RedisValue[] { "offline" });

            return (int)result == 1;
        }
    }
}