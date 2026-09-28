using System.Collections.Concurrent;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders;

/// <summary>
/// Per-order-number async lock. Must be registered as a singleton. Process-local only.
/// </summary>
public sealed class OrderNumberLockRegistry
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        var semaphore = _locks.GetOrAdd(orderNumber.Value, _ => new SemaphoreSlim(1, 1));
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
