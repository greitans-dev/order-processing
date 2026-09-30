using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
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
        VerifyChargeCalls(Moq.Times.Once);
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
    public async Task Process_UnknownOrder_ThrowsNotFound() =>
        await Should.ThrowAsync<OrderNotFoundException>(() =>
            Payments.ProcessAsync(new OrderNumber("missing"), default));

    [Fact]
    public async Task Process_PaidOrder_ReturnsAlreadyPaidWithoutCharging()
    {
        GatewaySucceeds();
        var order = SeedOrder("user-1", CreatedAt);
        await Payments.ProcessAsync(order.OrderNumber, default);

        var result = await Payments.ProcessAsync(order.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        VerifyChargeCalls(Moq.Times.Once);
    }

    [Fact]
    public async Task Process_Concurrent_ChargesGatewayOnce()
    {
        GatewaySucceedsSlowly();
        var order = SeedOrder("user-1", CreatedAt);

        var results = await RunConcurrently(() => Payments.ProcessAsync(order.OrderNumber, default));

        ShouldHaveOnePaidAndRestAlreadyPaid(results);
        VerifyChargeCalls(Moq.Times.Once);
    }
}
