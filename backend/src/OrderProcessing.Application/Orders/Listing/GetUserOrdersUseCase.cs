using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Queries;
using OrderProcessing.Application.Orders.Results;

namespace OrderProcessing.Application.Orders.Listing;

public sealed class GetUserOrdersUseCase(IOrderRepository repository)
{
    public async Task<IReadOnlyList<OrderSummaryDto>> ExecuteAsync(GetUserOrdersQuery query, CancellationToken ct)
    {
        var orders = await repository.FindByUserIdAsync(query.UserId, ct);
        return orders
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenBy(o => o.OrderNumber.Value).Select(OrderDtoMapper.ToSummary)
            .ToList();
    }
}
