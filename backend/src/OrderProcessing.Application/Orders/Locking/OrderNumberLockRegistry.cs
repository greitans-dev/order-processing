using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders.Locking;

/// <summary>
/// Per-key async lock (order numbers and idempotency keys). Must be registered as a singleton. Process-local only.
/// An entry exists only while someone holds or waits for its lock, so the registry does not grow with the number of
/// keys seen.
/// </summary>
public sealed class OrderNumberLockRegistry
{
    private readonly Dictionary<string, Entry> _entries = new();

    /// <summary>Number of keys currently held or waited for.</summary>
    public int ActiveLockCount
    {
        get
        {
            lock (_entries) return _entries.Count;
        }
    }

    public Task<IDisposable> AcquireAsync(OrderNumber orderNumber, CancellationToken ct) =>
        AcquireAsync($"order:{orderNumber.Value}", ct);

    public Task<IDisposable> AcquireAsync(string userId, IdempotencyKey key, CancellationToken ct) =>
        AcquireAsync($"idem:{userId.Length}:{userId}:{key.Value}", ct);

    private async Task<IDisposable> AcquireAsync(string lockKey, CancellationToken ct)
    {
        var entry = Retain(lockKey);
        try
        {
            await entry.Semaphore.WaitAsync(ct);
        }
        catch
        {
            Release(lockKey, entry);
            throw;
        }
        return new Releaser(this, lockKey, entry);
    }

    private Entry Retain(string lockKey)
    {
        lock (_entries)
        {
            if (!_entries.TryGetValue(lockKey, out var entry))
                _entries[lockKey] = entry = new Entry();
            entry.Users++;
            return entry;
        }
    }

    private void Release(string lockKey, Entry entry)
    {
        lock (_entries)
        {
            if (--entry.Users == 0) _entries.Remove(lockKey);
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Users { get; set; } // guarded by the registry's lock
    }

    private sealed class Releaser(OrderNumberLockRegistry registry, string lockKey, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            entry.Semaphore.Release();
            registry.Release(lockKey, entry);
        }
    }
}
