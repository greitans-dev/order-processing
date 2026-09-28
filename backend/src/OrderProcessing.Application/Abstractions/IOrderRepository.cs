using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> FindByOrderNumberAsync(OrderNumber orderNumber, CancellationToken ct);
    Task<IReadOnlyList<Order>> FindByUserIdAsync(string userId, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    Task UpdateAsync(Order order, CancellationToken ct);
}
