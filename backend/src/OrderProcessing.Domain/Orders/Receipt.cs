using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Domain.Orders;

public sealed record Receipt(
    OrderNumber OrderNumber,
    Money PaidAmount,
    DateTimeOffset PaidAtUtc,
    string PaymentConfirmation);
