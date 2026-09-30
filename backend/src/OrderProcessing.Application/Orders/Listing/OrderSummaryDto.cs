using OrderProcessing.Application.Orders.Payments;

namespace OrderProcessing.Application.Orders.Listing;

public sealed record OrderSummaryDto(
    string OrderNumber, decimal PayableAmount, string CurrencyCode, string PaymentGatewayId,
    string? Description, string Status, string? FailureReason, OrderReceiptDto? Receipt, DateTimeOffset CreatedAtUtc);
