namespace OrderProcessing.Application.Orders.Payments;

public sealed record OrderReceiptDto(
    string OrderNumber, decimal PaidAmount, string CurrencyCode, DateTimeOffset PaidAtUtc, string PaymentConfirmation);
