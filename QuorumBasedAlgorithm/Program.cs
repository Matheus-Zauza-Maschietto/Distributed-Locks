using QuorumBasedAlgorithm;
using StackExchange.Redis;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

string resource = "distributed_lock_resource";
TimeSpan lockExpiry = TimeSpan.FromSeconds(10);
var timeout = TimeSpan.FromSeconds(5);

await UseRedLockNet();

async Task SelfMadeRedlock()
{
    string[] redisEndpoints = new[] { "localhost:6379", "localhost:6380", "localhost:6381" };
    var redisConnections = new List<ConnectionMultiplexer>();

    foreach (var endpoint in redisEndpoints)
    {
        redisConnections.Add(await ConnectionMultiplexer.ConnectAsync(endpoint));
    }

    var redisDbs = new List<IDatabase>();
    foreach (var conn in redisConnections)
    {
        redisDbs.Add(conn.GetDatabase());
    }

    var redlock = new Redlock(redisDbs, lockExpiry, timeout);

    string lockId = await redlock.AcquireLockAsync(resource);

    if (lockId != null)
    {
        Console.WriteLine("Lock adquirido com sucesso!");

        await Task.Delay(5000);

        bool released = await redlock.ReleaseLockAsync(resource, lockId);
        Console.WriteLine(released ? "Lock liberado com sucesso." : "Falha ao liberar lock.");
    }
    else
    {
        Console.WriteLine("Falha ao adquirir o lock.");
    }
}

async Task UseRedLockNet()
{
    var endpoints = new List<RedLockEndPoint>
    {
        new RedLockEndPoint { EndPoint = new System.Net.DnsEndPoint("localhost", 6379) },
        new RedLockEndPoint { EndPoint = new System.Net.DnsEndPoint("localhost", 6380) },
        new RedLockEndPoint { EndPoint = new System.Net.DnsEndPoint("localhost", 6381) }
    };

    using (var redlockFactory = RedLockFactory.Create(endpoints))
    {                
        var retry = TimeSpan.FromSeconds(0.2);

        using (var redlock = await redlockFactory.CreateLockAsync(resource, lockExpiry, timeout, retry))
        {
            if (redlock.IsAcquired)
            {
                Console.WriteLine("RedLock.net: Lock adquirido com sucesso!");

                await Task.Delay(3000);

                Console.WriteLine("RedLock.net: Trabalho concluído, liberando o lock.");
                
            }
            else
            {
                Console.WriteLine("RedLock.net: Falha ao adquirir o lock.");
            }
        }
    }
}
