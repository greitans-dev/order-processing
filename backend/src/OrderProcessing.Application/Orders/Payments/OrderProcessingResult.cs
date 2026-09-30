namespace OrderProcessing.Application.Orders.Payments;

/// <summary>What processing an order ended in. Each case carries exactly the data that case has.</summary>
public abstract record OrderProcessingResult
{
    private OrderProcessingResult(string orderNumber) => OrderNumber = orderNumber;

    public string OrderNumber { get; }

    /// <summary>The order was charged now.</summary>
    public sealed record Paid(OrderReceiptDto Receipt) : OrderProcessingResult(Receipt.OrderNumber);

    /// <summary>The order had been paid before, so nothing was charged.</summary>
    public sealed record AlreadyPaid(OrderReceiptDto Receipt) : OrderProcessingResult(Receipt.OrderNumber);

    /// <summary>The gateway declined the payment.</summary>
    public sealed record Failed(OrderProcessingError Error) : OrderProcessingResult(Error.OrderNumber);
}
