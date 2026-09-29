using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Persistence;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests;

public class InMemoryOrderRepositoryTests
{
    private static Order NewOrder(string user = "u1", string? key = null) =>
        Order.Create(user, new IdempotencyKey(key ?? Guid.NewGuid().ToString()), Money.Of(10m, "EUR"),
            new PaymentGatewayId("mock-alpha"), null);

    [Fact]
    public async Task Add_then_find_roundtrips()
    {
        var repo = new InMemoryOrderRepository();
        var order = NewOrder();
        await repo.AddAsync(order, default);

        (await repo.FindByOrderNumberAsync(order.OrderNumber, default)).ShouldBeSameAs(order);
    }

    [Fact]
    public async Task Find_unknown_returns_null() =>
        (await new InMemoryOrderRepository().FindByOrderNumberAsync(new OrderNumber("ORD-X"), default)).ShouldBeNull();

    [Fact]
    public async Task Update_persists_state_change()
    {
        var repo = new InMemoryOrderRepository();
        var order = NewOrder();
        await repo.AddAsync(order, default);
        order.MarkFailed("nope");
        await repo.UpdateAsync(order, default);

        (await repo.FindByOrderNumberAsync(order.OrderNumber, default))!.Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task Update_of_unknown_order_throws() =>
        await Should.ThrowAsync<InvalidOperationException>(
            () => new InMemoryOrderRepository().UpdateAsync(NewOrder(), default));

    [Fact]
    public async Task FindByUserId_filters_by_user()
    {
        var repo = new InMemoryOrderRepository();
        await repo.AddAsync(NewOrder("a"), default);
        await repo.AddAsync(NewOrder("a"), default);
        await repo.AddAsync(NewOrder("b"), default);

        (await repo.FindByUserIdAsync("a", default)).Count.ShouldBe(2);
        (await repo.FindByUserIdAsync("zzz", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByIdempotencyKey_returns_the_order_for_that_user_and_key()
    {
        var repo = new InMemoryOrderRepository();
        var order = NewOrder("a", "k1");
        await repo.AddAsync(order, default);
        await repo.AddAsync(NewOrder("b", "k1"), default);

        (await repo.FindByIdempotencyKeyAsync("a", new IdempotencyKey("k1"), default)).ShouldBeSameAs(order);
        (await repo.FindByIdempotencyKeyAsync("a", new IdempotencyKey("other"), default)).ShouldBeNull();
    }

    [Fact]
    public async Task Add_with_duplicate_idempotency_key_for_same_user_throws()
    {
        var repo = new InMemoryOrderRepository();
        await repo.AddAsync(NewOrder("a", "k1"), default);

        await Should.ThrowAsync<InvalidOperationException>(() => repo.AddAsync(NewOrder("a", "k1"), default));
        (await repo.FindByUserIdAsync("a", default)).Count.ShouldBe(1);
    }
}
