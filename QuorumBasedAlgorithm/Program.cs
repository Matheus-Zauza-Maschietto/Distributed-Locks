using QuorumBasedAlgorithm;
using StackExchange.Redis;

var redisEndpoints = new[] { "localhost:6379", "localhost:6380", "localhost:6381" };
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

var redlock = new Redlock(redisDbs, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));

string resource = "distributed_lock_resource";
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