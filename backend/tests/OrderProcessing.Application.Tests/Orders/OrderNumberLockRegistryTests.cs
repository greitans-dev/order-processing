using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders;

public class OrderNumberLockRegistryTests
{
    [Fact]
    public async Task AcquireAsync_SameOrderNumber_BlocksUntilReleased()
    {
        var locks = new OrderNumberLockRegistry();
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
        var locks = new OrderNumberLockRegistry();
        using var first = await locks.AcquireAsync(new OrderNumber("ORD-1"), default);

        using var second = await locks.AcquireAsync(new OrderNumber("ORD-2"), default)
            .WaitAsync(TimeSpan.FromSeconds(1));
    }
}
