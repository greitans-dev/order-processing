using System.Collections.Concurrent;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Tests;

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public int UpdateCount;

    public Task<Order?> FindByOrderNumberAsync(OrderNumber orderNumber, CancellationToken ct) =>
        Task.FromResult(_orders.GetValueOrDefault(orderNumber.Value));

    public Task<IReadOnlyList<Order>> FindByUserIdAsync(string userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.Values.Where(o => o.UserId == userId).ToList());

    public Task<Order?> FindByIdempotencyKeyAsync(string userId, IdempotencyKey key, CancellationToken ct) =>
        Task.FromResult(_orders.Values.FirstOrDefault(o => o.UserId == userId && o.IdempotencyKey == key));

    public Task AddAsync(Order order, CancellationToken ct)
    {
        _orders[order.OrderNumber.Value] = order;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Order order, CancellationToken ct)
    {
        Interlocked.Increment(ref UpdateCount);
        _orders[order.OrderNumber.Value] = order;
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<Order> All => _orders.Values.ToList();
}
