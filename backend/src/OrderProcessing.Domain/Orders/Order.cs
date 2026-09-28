using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Domain.Orders;

public sealed class Order
{
    private Order(OrderNumber orderNumber, string userId, Money payableAmount,
        PaymentGatewayId paymentGatewayId, string? description)
    {
        OrderNumber = orderNumber;
        UserId = userId;
        PayableAmount = payableAmount;
        PaymentGatewayId = paymentGatewayId;
        Description = description;
        Status = OrderStatus.Pending;
    }

    public OrderNumber OrderNumber { get; }
    public string UserId { get; }
    public Money PayableAmount { get; }
    public PaymentGatewayId PaymentGatewayId { get; }
    public string? Description { get; }
    public OrderStatus Status { get; private set; }
    public Receipt? Receipt { get; private set; }
    public string? FailureReason { get; private set; }

    public static Order Create(string userId, Money payableAmount, PaymentGatewayId paymentGatewayId,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new Order(OrderNumber.New(), userId, payableAmount, paymentGatewayId, description);
    }

    public void MarkPaid(Receipt receipt)
    {
        EnsureNotPaid();
        Status = OrderStatus.Paid;
        Receipt = receipt;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        EnsureNotPaid();
        Status = OrderStatus.Failed;
        FailureReason = reason;
    }

    private void EnsureNotPaid()
    {
        if (Status == OrderStatus.Paid)
            throw new InvalidOperationException($"Order {OrderNumber} is already paid.");
    }
}
