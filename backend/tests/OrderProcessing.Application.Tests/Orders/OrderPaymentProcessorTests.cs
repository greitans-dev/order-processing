using Moq;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders;

public class OrderPaymentProcessorTests : OrderProcessingServiceTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Process_PendingOrderAndGatewaySucceeds_MarksPaidAndReturnsReceipt()
    {
        GatewaySucceeds();
        var order = SeedOrder("user-1", CreatedAt);

        var result = await Payments.ProcessAsync(order.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        order.Status.ShouldBe(OrderStatus.Paid);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task Process_PendingOrderAndGatewayDeclines_MarksFailed()
    {
        GatewayDeclines();
        var order = SeedOrder("user-1", CreatedAt);

        var result = await Payments.ProcessAsync(order.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        order.Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task Process_PendingOrder_PassesAmountAndDescriptionToGateway()
    {
        PaymentRequest? captured = null;
        SetupCharge()
            .Callback<PaymentRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(PaymentResult.Success("C"));
        var order = SeedOrder("user-1", CreatedAt, 12.34m, "desc");

        await Payments.ProcessAsync(order.OrderNumber, default);

        captured.ShouldNotBeNull();
        captured.Amount.Amount.ShouldBe(12.34m);
        captured.Description.ShouldBe("desc");
    }

    [Fact]
    public async Task Process_UnknownOrder_ThrowsNotFound() =>
        await Should.ThrowAsync<OrderNotFoundException>(() =>
            Payments.ProcessAsync(new OrderNumber("missing"), default));

    [Fact]
    public async Task Process_PaidOrder_ReturnsAlreadyPaidWithoutCharging()
    {
        GatewaySucceeds();
        var order = SeedOrder("user-1", CreatedAt);
        var first = await Payments.ProcessAsync(order.OrderNumber, default);

        var result = await Payments.ProcessAsync(order.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        result.Receipt.ShouldBe(first.Receipt);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task Process_Concurrent_ChargesGatewayOnce()
    {
        GatewaySucceedsSlowly();
        var order = SeedOrder("user-1", CreatedAt);

        var results = await RunConcurrently(() => Payments.ProcessAsync(order.OrderNumber, default));

        ShouldHaveOnePaidAndRestAlreadyPaid(results);
        VerifyChargeCalls(Times.Once);
        results.Select(r => r.Receipt!.PaymentConfirmation).Distinct().ShouldBe(["CONF-X"]);
        order.Status.ShouldBe(OrderStatus.Paid);
    }
}
