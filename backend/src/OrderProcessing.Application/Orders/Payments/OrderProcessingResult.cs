namespace OrderProcessing.Application.Orders.Payments;

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
