namespace OrderProcessing.Application.Orders.Results;

public sealed record OrderReceiptDto(
    string OrderNumber, decimal PaidAmount, string CurrencyCode, DateTimeOffset PaidAtUtc, string PaymentConfirmation);
