using Moq;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders.Payments;

public class OrderPaymentProcessorTests : OrderUseCaseTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Process_PendingOrderAndGatewaySucceeds_MarksPaidAndReturnsReceipt()
    {
        GatewaySucceeds();
        var order = SeedOrder("user-1", CreatedAt);

        var result = await Payments.ProcessAsync(order.OrderNumber, default);

        result.ShouldBeOfType<OrderProcessingResult.Paid>();
        order.Status.ShouldBe(OrderStatus.Paid);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task Process_PendingOrderAndGatewayDeclines_MarksFailed()
    {
        GatewayDeclines();
        var order = SeedOrder("user-1", CreatedAt);

        var result = await Payments.ProcessAsync(order.OrderNumber, default);

        result.ShouldBeOfType<OrderProcessingResult.Failed>();
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

    [Theory]
    [InlineData(true, OrderStatus.Paid)]
    [InlineData(false, OrderStatus.Failed)]
    public async Task Process_RequestCanceledDuringCharge_StillPersistsTheGatewayOutcome(bool gatewaySucceeds,
        OrderStatus expected)
    {
        using var cts = new CancellationTokenSource();
        SetupCharge().Returns(() =>
        {
            cts.Cancel();
            return Task.FromResult(gatewaySucceeds ? PaymentResult.Success("C") : PaymentResult.Failure("No"));
        });
        var order = SeedOrder("user-1", CreatedAt);

        await Payments.ProcessAsync(order.OrderNumber, cts.Token);

        var stored = await Repository.FindByOrderNumberAsync(order.OrderNumber, default);
        stored!.Status.ShouldBe(expected);
    }

    [Fact]
    public async Task Process_GatewayDoesNotAnswerInTime_ThrowsTimeoutAndLeavesOrderPending()
    {
        PaymentOptions.GatewayTimeout = TimeSpan.FromMilliseconds(50);
        SetupCharge().Returns(async (PaymentRequest _, CancellationToken token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return PaymentResult.Success("never");
        });
        var order = SeedOrder("user-1", CreatedAt);

        var ex = await Should.ThrowAsync<PaymentGatewayTimeoutException>(() =>
            Payments.ProcessAsync(order.OrderNumber, default));

        ex.OrderNumber.ShouldBe(order.OrderNumber);
        order.Status.ShouldBe(OrderStatus.Pending); // the outcome is unknown, so it is neither Paid nor Failed
    }

    [Fact]
    public async Task Process_CallerCancelsWhileGatewayIsSlow_ThrowsCancellationNotTimeout()
    {
        SetupCharge().Returns(async (PaymentRequest _, CancellationToken token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return PaymentResult.Success("never");
        });
        var order = SeedOrder("user-1", CreatedAt);
        using var cts = new CancellationTokenSource();

        var call = Payments.ProcessAsync(order.OrderNumber, cts.Token);
        await cts.CancelAsync();

        var ex = await Should.ThrowAsync<OperationCanceledException>(() => call);
        ex.ShouldNotBeOfType<PaymentGatewayTimeoutException>();
        order.Status.ShouldBe(OrderStatus.Pending);
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

        result.ShouldBeOfType<OrderProcessingResult.AlreadyPaid>().Receipt
            .ShouldBe(first.ShouldBeOfType<OrderProcessingResult.Paid>().Receipt);
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
        results.Select(r => ReceiptOf(r).PaymentConfirmation).Distinct().ShouldBe(["CONF-X"]);
        order.Status.ShouldBe(OrderStatus.Paid);
    }
}
