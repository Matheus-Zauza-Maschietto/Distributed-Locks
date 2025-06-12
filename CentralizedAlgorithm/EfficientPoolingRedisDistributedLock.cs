using System;
using StackExchange.Redis;

namespace CentralizedAlgorithm;

public class EfficientPoolingRedisDistributedLock
{
    private readonly IDatabase _redisDb;
    private readonly ISubscriber _subscriber;
    private readonly string _lockKey;
    private readonly TimeSpan _lockExpiry;
    private bool _isTryingToAcquireLock = false;
    private TaskCompletionSource<bool>? _acquireLockTask;
    public EfficientPoolingRedisDistributedLock(ConnectionMultiplexer redis, string lockKey, TimeSpan lockExpiry)
    {
        _redisDb = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _lockKey = lockKey;
        _lockExpiry = lockExpiry;
        _subscriber.Subscribe(RedisChannel.Literal($"{_lockKey ?? throw new ArgumentNullException(nameof(_lockKey))}-channel"), async (channel, message) =>
        {
            if (_isTryingToAcquireLock) 
            {
                _isTryingToAcquireLock = false;
                if (_acquireLockTask is TaskCompletionSource<bool>)
                {
                    _acquireLockTask.SetResult(true);
                }
            }
        });
    }

    public async Task<bool> AcquireLockAsync(string lockValue)
    {
        bool acquired = false;
        do
        {
            acquired = await _redisDb.StringSetAsync(_lockKey, lockValue, _lockExpiry, When.NotExists);
            if (!acquired)
            {
                await AwaitToTryAcquiteLockAgainAsync();
            }

        } while (!acquired);
        return acquired;
    }

    public async Task<bool> AwaitToTryAcquiteLockAgainAsync()
    {
        _isTryingToAcquireLock = true;
        var tcs = new TaskCompletionSource<bool>();
        _acquireLockTask = tcs;
        return await _acquireLockTask.Task;
    }

    public async Task<bool> ReleaseLockAsync(string lockValue)
    {
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        bool result = (int)await _redisDb.ScriptEvaluateAsync(script, new RedisKey[] { _lockKey }, new RedisValue[] { lockValue }) == 1;
        if (result) _redisDb.PublishAsync(RedisChannel.Literal($"{_lockKey ?? throw new ArgumentNullException(nameof(_lockKey))}-channel"), "released");
        _isTryingToAcquireLock = false;
        _acquireLockTask = null;
        return result;
    }
}
