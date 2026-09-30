using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Locking;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Orders.Submit;

public sealed partial class SubmitOrderUseCase(
    IOrderRepository repository,
    IPaymentGatewayRegistry gatewayRegistry,
    OrderLockRegistry locks,
    OrderPaymentProcessor payments,
    TimeProvider clock,
    ILogger<SubmitOrderUseCase> logger)
{
    public async Task<OrderProcessingResult> ExecuteAsync(SubmitOrderCommand command, CancellationToken ct)
    {
        var gatewayId = new PaymentGatewayId(command.PaymentGatewayId);
        gatewayRegistry.Resolve(gatewayId); // fail fast: never persist an order for an unknown gateway
        var amount = Money.Of(command.PayableAmount, command.CurrencyCode);

        using var _ = await locks.AcquireAsync(command.UserId, command.IdempotencyKey, ct);
        var existingOrder = await repository.FindByIdempotencyKeyAsync(command.UserId, command.IdempotencyKey, ct);
        if (existingOrder is not null)
        {
            if (!existingOrder.MatchesRequest(command.UserId, amount, gatewayId, command.Description))
            {
                LogIdempotencyKeyConflict(command.IdempotencyKey.Value, command.UserId, existingOrder.OrderNumber.Value);
                throw new IdempotencyKeyReuseException(command.IdempotencyKey);
            }
            LogIdempotentReplay(command.UserId, existingOrder.OrderNumber.Value, existingOrder.Status.ToString());
            // Replay: a failed order is only retried through resubmit, never by repeating the key.
            return existingOrder.Status == OrderStatus.Failed
                ? new OrderProcessingResult.Failed(
                    new OrderProcessingError(existingOrder.OrderNumber.Value, existingOrder.FailureReason!))
                : await payments.ProcessAsync(existingOrder.OrderNumber, ct);
        }

        var newOrder = Order.Create(command.UserId, command.IdempotencyKey, clock.GetUtcNow(), amount, gatewayId,
            command.Description);
        await repository.AddAsync(newOrder, ct);
        LogOrderCreated(newOrder.OrderNumber.Value, newOrder.UserId, amount.Amount, amount.CurrencyCode, gatewayId.Value);
        return await payments.ProcessAsync(newOrder.OrderNumber, ct);
    }
}
