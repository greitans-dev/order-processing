using OrderProcessing.Application.Orders.Locking;
using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders.Locking;

public class OrderLockRegistryTests
{
    [Fact]
    public async Task AcquireAsync_SameOrderNumber_BlocksUntilReleased()
    {
        var locks = new OrderLockRegistry();
        var number = new OrderNumber("ORD-1");
        using var first = await locks.AcquireAsync(number, default);

        var second = locks.AcquireAsync(number, default);

        await Task.Delay(50);
        second.IsCompleted.ShouldBeFalse();
        first.Dispose();
        (await second).Dispose();
    }

    [Fact]
    public async Task AcquireAsync_DifferentOrderNumbers_DoNotBlock()
    {
        var locks = new OrderLockRegistry();
        using var first = await locks.AcquireAsync(new OrderNumber("ORD-1"), default);

        using var second = await locks.AcquireAsync(new OrderNumber("ORD-2"), default)
            .WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AcquireAsync_AfterRelease_DoesNotRetainTheLock()
    {
        var locks = new OrderLockRegistry();
        var first = await locks.AcquireAsync(new OrderNumber("ORD-1"), default);
        var second = await locks.AcquireAsync("alice", new IdempotencyKey("key-1"), default);

        first.Dispose();
        second.Dispose();

        locks.ActiveLockCount.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_CanceledWhileWaiting_DoesNotRetainTheLock()
    {
        var locks = new OrderLockRegistry();
        var number = new OrderNumber("ORD-1");
        var held = await locks.AcquireAsync(number, default);
        using var cts = new CancellationTokenSource();
        var waiting = locks.AcquireAsync(number, cts.Token);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(waiting);
        held.Dispose();

        locks.ActiveLockCount.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_OrderNumberLookingLikeIdempotencyLockKey_DoesNotBlockTheIdempotencyLock()
    {
        var locks = new OrderLockRegistry();
        using var orderLock = await locks.AcquireAsync(new OrderNumber("idem:5:alice:key-1"), default);

        using var idempotencyLock = await locks.AcquireAsync("alice", new IdempotencyKey("key-1"), default)
            .WaitAsync(TimeSpan.FromSeconds(1));
    }
}
