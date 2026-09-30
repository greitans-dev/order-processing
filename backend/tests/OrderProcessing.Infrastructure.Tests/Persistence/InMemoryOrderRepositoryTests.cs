using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Persistence;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests.Persistence;

public class InMemoryOrderRepositoryTests
{
    private readonly InMemoryOrderRepository _repository = new();

    private static Order NewOrder(string user = "u1", string? key = null) =>
        Order.Create(user, new IdempotencyKey(key ?? Guid.NewGuid().ToString()), DateTimeOffset.UtcNow, Money.Of(10m, "EUR"),
            new PaymentGatewayId("mock-alpha"), null);

    [Fact]
    public async Task AddAsync_NewOrder_CanBeFoundByOrderNumber()
    {
        var order = NewOrder();
        await _repository.AddAsync(order, default);

        (await _repository.FindByOrderNumberAsync(order.OrderNumber, default)).ShouldBeSameAs(order);
    }

    [Fact]
    public async Task FindByOrderNumberAsync_UnknownOrder_ReturnsNull() =>
        (await _repository.FindByOrderNumberAsync(new OrderNumber("ORD-X"), default)).ShouldBeNull();

    [Fact]
    public async Task UpdateAsync_ChangedOrder_PersistsStateChange()
    {
        var order = NewOrder();
        await _repository.AddAsync(order, default);
        order.MarkFailed("nope");
        await _repository.UpdateAsync(order, default);

        (await _repository.FindByOrderNumberAsync(order.OrderNumber, default))!.Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task UpdateAsync_UnknownOrder_Throws() =>
        await Should.ThrowAsync<InvalidOperationException>(() => _repository.UpdateAsync(NewOrder(), default));

    [Fact]
    public async Task FindByUserIdAsync_MultipleUsers_ReturnsOnlyThatUsersOrders()
    {
        await _repository.AddAsync(NewOrder("a"), default);
        await _repository.AddAsync(NewOrder("a"), default);
        await _repository.AddAsync(NewOrder("b"), default);

        (await _repository.FindByUserIdAsync("a", default)).Count.ShouldBe(2);
        (await _repository.FindByUserIdAsync("zzz", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByIdempotencyKeyAsync_MatchingUserAndKey_ReturnsOrder()
    {
        var order = NewOrder("a", "k1");
        await _repository.AddAsync(order, default);
        await _repository.AddAsync(NewOrder("b", "k1"), default);

        (await _repository.FindByIdempotencyKeyAsync("a", new IdempotencyKey("k1"), default)).ShouldBeSameAs(order);
        (await _repository.FindByIdempotencyKeyAsync("a", new IdempotencyKey("other"), default)).ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_DuplicateKeyForSameUser_Throws()
    {
        await _repository.AddAsync(NewOrder("a", "k1"), default);

        await Should.ThrowAsync<InvalidOperationException>(() => _repository.AddAsync(NewOrder("a", "k1"), default));
        (await _repository.FindByUserIdAsync("a", default)).Count.ShouldBe(1);
    }
}
