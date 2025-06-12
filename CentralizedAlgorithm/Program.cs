using CentralizedAlgorithm;
using StackExchange.Redis;

var redis = ConnectionMultiplexer.Connect("localhost");

var lockKey = "resource-lock";
var lockExpiry = TimeSpan.FromSeconds(20);
var lockValue = Guid.NewGuid().ToString();
await EfficientPolling();


async Task EfficientPolling()
{
    var distributedLock = new EfficientPoolingRedisDistributedLock(redis, lockKey, lockExpiry);

    bool acquired = false;
    while (!acquired)
    {
        acquired = await distributedLock.AcquireLockAsync(lockValue);
        if (!acquired)
        {
            throw new Exception("Erro ao adquirir o lock");
        }

        Console.WriteLine("Lock adquirido com sucesso!");
        await Task.Delay(5000);
        await distributedLock.ReleaseLockAsync(lockValue);
        System.Console.WriteLine("Lock liberado com sucesso!");
    }
}

async Task AgressivePolling()
{
    var distributedLock = new AgressivePoolingRedisDistributedLock(redis.GetDatabase(), lockKey, lockExpiry);

    bool acquired = false;
    do
    {
        acquired = await distributedLock.AcquireLockAsync(lockValue);
        if (!acquired)
        {
            Console.WriteLine("Lock não adquirido com. Aguardando para tentar novamente...");
            await Task.Delay(1000);
        }
        else
        {
            Console.WriteLine("Lock adquirido com sucesso!");
            await Task.Delay(10000);
            await distributedLock.ReleaseLockAsync(lockValue);
        }
    } while (!acquired);
}
;
