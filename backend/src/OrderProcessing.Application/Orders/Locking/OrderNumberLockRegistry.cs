using System.Collections.Concurrent;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders.Locking;

/// <summary>
/// Per-key async lock (order numbers and idempotency keys). Must be registered as a singleton. Process-local only.
/// </summary>
public sealed class OrderNumberLockRegistry
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public Task<IDisposable> AcquireAsync(OrderNumber orderNumber, CancellationToken ct) =>
        AcquireAsync(orderNumber.Value, ct);

    public Task<IDisposable> AcquireAsync(string userId, IdempotencyKey key, CancellationToken ct) =>
        AcquireAsync($"idem:{userId.Length}:{userId}:{key.Value}", ct);

    private async Task<IDisposable> AcquireAsync(string lockKey, CancellationToken ct)
    {
        var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) semaphore.Release();
        }
    }
}
