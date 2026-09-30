using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Persistence;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests;

public class InMemoryOrderRepositoryTests
{
    private static Order NewOrder(string user = "u1", string? key = null) =>
        Order.Create(user, new IdempotencyKey(key ?? Guid.NewGuid().ToString()), DateTimeOffset.UtcNow, Money.Of(10m, "EUR"),
            new PaymentGatewayId("mock-alpha"), null);

    [Fact]
    public async Task AddAsync_NewOrder_CanBeFoundByOrderNumber()
    {
        var repo = new InMemoryOrderRepository();
        var order = NewOrder();
        await repo.AddAsync(order, default);

        (await repo.FindByOrderNumberAsync(order.OrderNumber, default)).ShouldBeSameAs(order);
    }

    [Fact]
    public async Task FindByOrderNumberAsync_UnknownOrder_ReturnsNull() =>
        (await new InMemoryOrderRepository().FindByOrderNumberAsync(new OrderNumber("ORD-X"), default)).ShouldBeNull();

    [Fact]
    public async Task UpdateAsync_ChangedOrder_PersistsStateChange()
    {
        var repo = new InMemoryOrderRepository();
        var order = NewOrder();
        await repo.AddAsync(order, default);
        order.MarkFailed("nope");
        await repo.UpdateAsync(order, default);

        (await repo.FindByOrderNumberAsync(order.OrderNumber, default))!.Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task UpdateAsync_UnknownOrder_Throws() =>
        await Should.ThrowAsync<InvalidOperationException>(
            () => new InMemoryOrderRepository().UpdateAsync(NewOrder(), default));

    [Fact]
    public async Task FindByUserIdAsync_MultipleUsers_ReturnsOnlyThatUsersOrders()
    {
        var repo = new InMemoryOrderRepository();
        await repo.AddAsync(NewOrder("a"), default);
        await repo.AddAsync(NewOrder("a"), default);
        await repo.AddAsync(NewOrder("b"), default);

        (await repo.FindByUserIdAsync("a", default)).Count.ShouldBe(2);
        (await repo.FindByUserIdAsync("zzz", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByIdempotencyKeyAsync_MatchingUserAndKey_ReturnsOrder()
    {
        var repo = new InMemoryOrderRepository();
        var order = NewOrder("a", "k1");
        await repo.AddAsync(order, default);
        await repo.AddAsync(NewOrder("b", "k1"), default);

        (await repo.FindByIdempotencyKeyAsync("a", new IdempotencyKey("k1"), default)).ShouldBeSameAs(order);
        (await repo.FindByIdempotencyKeyAsync("a", new IdempotencyKey("other"), default)).ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_DuplicateKeyForSameUser_Throws()
    {
        var repo = new InMemoryOrderRepository();
        await repo.AddAsync(NewOrder("a", "k1"), default);

        await Should.ThrowAsync<InvalidOperationException>(() => repo.AddAsync(NewOrder("a", "k1"), default));
        (await repo.FindByUserIdAsync("a", default)).Count.ShouldBe(1);
    }
}
