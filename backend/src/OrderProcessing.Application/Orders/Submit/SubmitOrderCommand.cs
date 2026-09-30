using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders.Submit;

public sealed record SubmitOrderCommand(
    string UserId, decimal PayableAmount, string CurrencyCode, string PaymentGatewayId, string? Description,
    IdempotencyKey IdempotencyKey);
