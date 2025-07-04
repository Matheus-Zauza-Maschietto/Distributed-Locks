using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace QuorumBasedAlgorithm;

public class Redlock
{
private readonly List<IDatabase> _redisDbs;
    private readonly TimeSpan _lockExpiry;
    private readonly int _quorum;
    private readonly TimeSpan _timeout;

    public Redlock(List<IDatabase> redisDbs, TimeSpan lockExpiry, TimeSpan timeout)
    {
        _redisDbs = redisDbs;
        _lockExpiry = lockExpiry;
        _timeout = timeout;
        _quorum = (redisDbs.Count / 2) + 1;
    }



    public async Task<string> AcquireLockAsync(string resource)
    {
        try
        {
            var token = new CancellationTokenSource(_timeout).Token;
            var lockId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            int successCount = 0;

            foreach (var db in _redisDbs)
            {
                try
                {
                    bool acquired = await db.StringSetAsync(resource, lockId, _lockExpiry, When.NotExists);
                    if (acquired)
                        successCount++;
                }
                catch { }
                token.ThrowIfCancellationRequested();
            }

            var elapsed = DateTime.UtcNow - startTime;

            token.ThrowIfCancellationRequested();
            if (successCount >= _quorum && elapsed < _lockExpiry)
            {
                return lockId;
            }
            else
            {
                await ReleaseLockAsync(resource, lockId);
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error acquiring lock: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ReleaseLockAsync(string resource, string lockId)
    {
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        int releasedCount = 0;

        foreach (var db in _redisDbs)
        {
            try
            {
                var result = (int)await db.ScriptEvaluateAsync(script, new RedisKey[] { resource }, new RedisValue[] { lockId });
                if (result == 1)
                    releasedCount++;
            }
            catch{}
        }

        return releasedCount >= _quorum;
    }
}
