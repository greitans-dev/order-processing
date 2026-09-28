using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Orders;

public sealed class OrderProcessingService(
    IOrderRepository repository,
    IPaymentGatewayRegistry gatewayRegistry,
    OrderNumberLockRegistry locks)
{
    public async Task<OrderProcessingResult> SubmitNewOrderAsync(SubmitOrderCommand command, CancellationToken ct)
    {
        var gatewayId = new PaymentGatewayId(command.PaymentGatewayId);
        gatewayRegistry.Resolve(gatewayId); // fail fast: never persist an order for an unknown gateway
        var order = Order.Create(command.UserId, Money.Of(command.PayableAmount, command.CurrencyCode),
            gatewayId, command.Description);
        await repository.AddAsync(order, ct);
        return await ProcessPaymentAsync(order.OrderNumber, ct);
    }

    public Task<OrderProcessingResult> ResubmitOrderAsync(OrderNumber orderNumber, CancellationToken ct) =>
        ProcessPaymentAsync(orderNumber, ct);

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersForUserAsync(string userId, CancellationToken ct)
    {
        var orders = await repository.FindByUserIdAsync(userId, ct);
        return orders.Select(ToSummary).ToList();
    }

    private async Task<OrderProcessingResult> ProcessPaymentAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(orderNumber, ct);
        var order = await repository.FindByOrderNumberAsync(orderNumber, ct)
            ?? throw new OrderNotFoundException(orderNumber);

        if (order.Status == OrderStatus.Paid)
            return OrderProcessingResult.AlreadyPaid(ToDto(order.Receipt!));

        var gateway = gatewayRegistry.Resolve(order.PaymentGatewayId);
        var result = await gateway.ChargeAsync(
            new PaymentRequest(order.OrderNumber, order.PayableAmount, order.Description), ct);

        if (result.IsSuccess)
        {
            var receipt = new Receipt(order.OrderNumber, order.PayableAmount, DateTimeOffset.UtcNow,
                result.ConfirmationCode!);
            order.MarkPaid(receipt);
            await repository.UpdateAsync(order, ct);
            return OrderProcessingResult.Success(ToDto(receipt));
        }

        order.MarkFailed(result.FailureReason!);
        await repository.UpdateAsync(order, ct);
        return OrderProcessingResult.Failure(order.OrderNumber.Value, result.FailureReason!);
    }

    private static OrderReceiptDto ToDto(Receipt r) =>
        new(r.OrderNumber.Value, r.PaidAmount.Amount, r.PaidAmount.CurrencyCode, r.PaidAtUtc, r.PaymentConfirmation);

    private static OrderSummaryDto ToSummary(Order o) =>
        new(o.OrderNumber.Value, o.PayableAmount.Amount, o.PayableAmount.CurrencyCode, o.PaymentGatewayId.Value,
            o.Description, o.Status.ToString(), o.FailureReason, o.Receipt is null ? null : ToDto(o.Receipt));
}
