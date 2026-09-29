using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests;

public class OrderTests
{
    private static readonly IdempotencyKey Key = new("key-1");

    private static Order NewOrder() =>
        Order.Create("user-1", Key, Money.Of(50m, "EUR"), new PaymentGatewayId("mock-alpha"), "notes");

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
        order.IdempotencyKey.ShouldBe(Key);
    }

    [Fact]
    public void Create_rejects_blank_user() =>
        Should.Throw<ArgumentException>(() =>
            Order.Create(" ", Key, Money.Of(1m, "EUR"), new PaymentGatewayId("g"), null));

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

    [Fact]
    public void MatchesRequest_is_true_for_identical_request() =>
        NewOrder().MatchesRequest("user-1", Money.Of(50m, "EUR"), new PaymentGatewayId("mock-alpha"), "notes")
            .ShouldBeTrue();

    [Theory]
    [InlineData("user-2", 50, "mock-alpha", "notes")]
    [InlineData("user-1", 51, "mock-alpha", "notes")]
    [InlineData("user-1", 50, "mock-beta", "notes")]
    [InlineData("user-1", 50, "mock-alpha", "other")]
    [InlineData("user-1", 50, "mock-alpha", null)]
    public void MatchesRequest_is_false_when_any_field_differs(string user, decimal amount, string gateway,
        string? description) =>
        NewOrder().MatchesRequest(user, Money.Of(amount, "EUR"), new PaymentGatewayId(gateway), description)
            .ShouldBeFalse();
}
