namespace OrderProcessing.Application.Orders;

public sealed record SubmitOrderCommand(
    string UserId, decimal PayableAmount, string CurrencyCode, string PaymentGatewayId, string? Description);

public sealed record OrderReceiptDto(
    string OrderNumber, decimal PaidAmount, string CurrencyCode, DateTimeOffset PaidAtUtc, string PaymentConfirmation);

public sealed record OrderProcessingError(string OrderNumber, string Message);

public sealed record OrderSummaryDto(
    string OrderNumber, decimal PayableAmount, string CurrencyCode, string PaymentGatewayId,
    string? Description, string Status, string? FailureReason, OrderReceiptDto? Receipt);

public enum OrderProcessingOutcome { Paid, AlreadyPaid, Failed }

public sealed record OrderProcessingResult
{
    public OrderProcessingOutcome Outcome { get; private init; }
    public string OrderNumber { get; private init; } = "";
    public OrderReceiptDto? Receipt { get; private init; }
    public OrderProcessingError? Error { get; private init; }

    public static OrderProcessingResult Success(OrderReceiptDto receipt) =>
        new() { Outcome = OrderProcessingOutcome.Paid, OrderNumber = receipt.OrderNumber, Receipt = receipt };

    public static OrderProcessingResult AlreadyPaid(OrderReceiptDto receipt) =>
        new() { Outcome = OrderProcessingOutcome.AlreadyPaid, OrderNumber = receipt.OrderNumber, Receipt = receipt };

    public static OrderProcessingResult Failure(string orderNumber, string message) =>
        new()
        {
            Outcome = OrderProcessingOutcome.Failed,
            OrderNumber = orderNumber,
            Error = new OrderProcessingError(orderNumber, message)
        };
}
