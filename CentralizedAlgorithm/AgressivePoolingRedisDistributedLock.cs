using System;
using StackExchange.Redis;

namespace CentralizedAlgorithm;

public class AgressivePoolingRedisDistributedLock
{
    private readonly IDatabase _redisDb;
    private readonly string _lockKey;
    private readonly TimeSpan _lockExpiry;

    public AgressivePoolingRedisDistributedLock(IDatabase redisDb, string lockKey, TimeSpan lockExpiry)
    {
        _redisDb = redisDb;
        _lockKey = lockKey;
        _lockExpiry = lockExpiry;
    }

    public async Task<bool> AcquireLockAsync(string lockValue)
    {
        return await _redisDb.StringSetAsync(_lockKey, lockValue, _lockExpiry, When.NotExists);
    }

    public async Task<bool> ReleaseLockAsync(string lockValue)
    {
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = (int)await _redisDb.ScriptEvaluateAsync(script, new RedisKey[] { _lockKey }, new RedisValue[] { lockValue });
        return result == 1;
    }
}
