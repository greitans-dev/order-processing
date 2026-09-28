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
