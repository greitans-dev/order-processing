namespace OrderProcessing.Application.Orders;

public sealed record OrderReceiptDto(
    string OrderNumber, decimal PaidAmount, string CurrencyCode, DateTimeOffset PaidAtUtc, string PaymentConfirmation);
