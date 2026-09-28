using System.Collections.Concurrent;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public Task<Order?> FindByOrderNumberAsync(OrderNumber orderNumber, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderNumber.Value));

    public Task<IReadOnlyList<Order>> FindByUserIdAsync(string userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.Values.Where(o => o.UserId == userId).ToList());

    public Task AddAsync(Order order, CancellationToken ct)
    {
        if (!_orders.TryAdd(order.OrderNumber.Value, order))
            throw new InvalidOperationException($"Order {order.OrderNumber} already exists.");
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
