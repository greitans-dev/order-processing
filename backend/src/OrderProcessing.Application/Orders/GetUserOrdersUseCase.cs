using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Application.Orders;

public sealed class GetUserOrdersUseCase(IOrderRepository repository)
{
    public async Task<IReadOnlyList<OrderSummaryDto>> ExecuteAsync(string userId, CancellationToken ct)
    {
        var orders = await repository.FindByUserIdAsync(userId, ct);
        return orders
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenBy(o => o.OrderNumber.Value).Select(OrderDtoMapper.ToSummary)
            .ToList();
    }
}
