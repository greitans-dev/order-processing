using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Abstractions;

public sealed class UnknownPaymentGatewayException(string gatewayId)
    : Exception($"Payment gateway '{gatewayId}' is not available.")
{
    public string GatewayId { get; } = gatewayId;
}

public sealed class OrderNotFoundException(OrderNumber orderNumber)
    : Exception($"Order '{orderNumber}' was not found.")
{
    public OrderNumber OrderNumber { get; } = orderNumber;
}

public sealed class IdempotencyKeyReuseException(IdempotencyKey key)
    : Exception($"Idempotency key '{key}' was already used with a different order.")
{
    public IdempotencyKey Key { get; } = key;
}

/// <summary>
/// The gateway did not answer in time. The charge may still have gone through, so the order is left pending.
/// </summary>
public sealed class PaymentGatewayTimeoutException(OrderNumber orderNumber, string gatewayId, TimeSpan timeout)
    : Exception($"Payment gateway '{gatewayId}' did not answer within {timeout.TotalSeconds:0.##} seconds.")
{
    public OrderNumber OrderNumber { get; } = orderNumber;
    public string GatewayId { get; } = gatewayId;
}
