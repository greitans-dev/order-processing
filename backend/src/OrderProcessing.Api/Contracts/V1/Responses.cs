namespace OrderProcessing.Api.Contracts.V1;

public sealed record OrderReceiptResponse(
    string OrderNumber, decimal PaidAmount, string CurrencyCode, DateTimeOffset PaidAtUtc, string PaymentConfirmation);

public sealed record OrderErrorResponse(string OrderNumber, string Message);

public sealed record OrderSummaryResponse(
    string OrderNumber, decimal PayableAmount, string CurrencyCode, string PaymentGatewayId,
    string? Description, string Status, string? FailureReason, OrderReceiptResponse? Receipt);

public sealed record PaymentGatewayResponse(string Id, string Name);

public sealed record CurrencyResponse(string Code, string Name);
