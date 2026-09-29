using System.Collections.Concurrent;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();
    private readonly ConcurrentDictionary<(string UserId, string Key), string> _orderNumbersByKey = new();

    public Task<Order?> FindByOrderNumberAsync(OrderNumber orderNumber, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderNumber.Value));

    public Task<Order?> FindByIdempotencyKeyAsync(string userId, IdempotencyKey key, CancellationToken ct) =>
        Task.FromResult(_orderNumbersByKey.TryGetValue((userId, key.Value), out var orderNumber)
            ? _orders.GetValueOrDefault(orderNumber)
            : null);

    public Task<IReadOnlyList<Order>> FindByUserIdAsync(string userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.Values.Where(o => o.UserId == userId).ToList());

    public Task AddAsync(Order order, CancellationToken ct)
    {
        var keyIndex = (order.UserId, order.IdempotencyKey.Value);
        if (!_orderNumbersByKey.TryAdd(keyIndex, order.OrderNumber.Value))
            throw new InvalidOperationException(
                $"Idempotency key '{order.IdempotencyKey}' is already used by user '{order.UserId}'.");
        if (!_orders.TryAdd(order.OrderNumber.Value, order))
        {
            _orderNumbersByKey.TryRemove(keyIndex, out _);
            throw new InvalidOperationException($"Order {order.OrderNumber} already exists.");
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Order order, CancellationToken ct)
    {
        if (!_orders.ContainsKey(order.OrderNumber.Value))
            throw new InvalidOperationException($"Order {order.OrderNumber} does not exist.");
        _orders[order.OrderNumber.Value] = order;
        return Task.CompletedTask;
    }
}
