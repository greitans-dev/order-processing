using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests;

public class OrderTests
{
    private static Order NewOrder() =>
        Order.Create("user-1", Money.Of(50m, "EUR"), new PaymentGatewayId("mock-alpha"), "notes");

    private static Receipt ReceiptFor(Order o) =>
        new(o.OrderNumber, o.PayableAmount, DateTimeOffset.UtcNow, "CONF-1");

    [Fact]
    public void Create_starts_pending_with_generated_number()
    {
        var order = NewOrder();
        order.Status.ShouldBe(OrderStatus.Pending);
        order.OrderNumber.Value.ShouldStartWith("ORD-");
        order.Receipt.ShouldBeNull();
        order.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void Create_rejects_blank_user() =>
        Should.Throw<ArgumentException>(() =>
            Order.Create(" ", Money.Of(1m, "EUR"), new PaymentGatewayId("g"), null));

    [Fact]
    public void MarkPaid_sets_status_and_receipt()
    {
        var order = NewOrder();
        var receipt = ReceiptFor(order);
        order.MarkPaid(receipt);
        order.Status.ShouldBe(OrderStatus.Paid);
        order.Receipt.ShouldBe(receipt);
    }

    [Fact]
    public void MarkFailed_sets_status_and_reason()
    {
        var order = NewOrder();
        order.MarkFailed("declined");
        order.Status.ShouldBe(OrderStatus.Failed);
        order.FailureReason.ShouldBe("declined");
    }

    [Fact]
    public void Failed_order_can_be_paid_later()
    {
        var order = NewOrder();
        order.MarkFailed("declined");
        order.MarkPaid(ReceiptFor(order));
        order.Status.ShouldBe(OrderStatus.Paid);
        order.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void MarkPaid_after_paid_throws()
    {
        var order = NewOrder();
        order.MarkPaid(ReceiptFor(order));
        Should.Throw<InvalidOperationException>(() => order.MarkPaid(ReceiptFor(order)));
    }

    [Fact]
    public void MarkFailed_after_paid_throws()
    {
        var order = NewOrder();
        order.MarkPaid(ReceiptFor(order));
        Should.Throw<InvalidOperationException>(() => order.MarkFailed("x"));
        order.Status.ShouldBe(OrderStatus.Paid);
    }
}
