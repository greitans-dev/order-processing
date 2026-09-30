using Moq;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders;

public class OrderProcessingServiceTests : OrderProcessingServiceTestBase
{
    [Fact]
    public async Task SubmitNewOrder_GatewaySucceeds_ReturnsReceiptAndMarksPaid()
    {
        GatewaySucceeds();

        var result = await Sut.SubmitNewOrderAsync(Command(42.5m), default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        result.Receipt.ShouldNotBeNull();
        result.Receipt.PaidAmount.ShouldBe(42.5m);
        result.Receipt.CurrencyCode.ShouldBe("EUR");
        result.Receipt.PaymentConfirmation.ShouldBe("CONF-1");
        result.Receipt.OrderNumber.ShouldBe(result.OrderNumber);
        Repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task SubmitNewOrder_GatewayDeclines_ReturnsErrorAndMarksFailed()
    {
        GatewayDeclines();

        var result = await Sut.SubmitNewOrderAsync(Command(), default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        result.Error.ShouldNotBeNull().Message.ShouldBe("Declined: limit");
        result.OrderNumber.ShouldStartWith("ORD-");
        Repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task SubmitNewOrder_UnknownGateway_ThrowsAndPersistsNothing()
    {
        await Should.ThrowAsync<UnknownPaymentGatewayException>(
            () => Sut.SubmitNewOrderAsync(Command(gateway: "ghost"), default));
        Repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitNewOrder_UnsupportedCurrency_ThrowsAndPersistsNothing()
    {
        await Should.ThrowAsync<UnsupportedCurrencyException>(
            () => Sut.SubmitNewOrderAsync(Command(currency: "USD"), default));
        Repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResubmitOrder_FailedOrderAndGatewaySucceeds_PaysAndReusesOrderNumber()
    {
        var failed = await SeedFailedOrder();
        GatewaySucceeds();

        var result = await Sut.ResubmitOrderAsync(failed.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        result.OrderNumber.ShouldBe(failed.OrderNumber.Value);
        Repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyTwice_CreatesOneOrderAndChargesOnce()
    {
        GatewaySucceeds();

        var first = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        var second = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);

        first.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        second.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        second.OrderNumber.ShouldBe(first.OrderNumber);
        second.Receipt.ShouldBe(first.Receipt);
        Repository.All.Count.ShouldBe(1);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayOfFailedOrder_ReturnsSameFailureWithoutCharging()
    {
        GatewayDeclines();

        var first = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        var second = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);

        second.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        second.OrderNumber.ShouldBe(first.OrderNumber);
        second.Error.ShouldBe(first.Error);
        Repository.All.Count.ShouldBe(1);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayAfterSuccessfulResubmit_ReturnsAlreadyPaid()
    {
        GatewayDeclines();
        var failed = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        GatewaySucceeds();
        await Sut.ResubmitOrderAsync(new OrderNumber(failed.OrderNumber), default);

        var replay = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);

        replay.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        replay.OrderNumber.ShouldBe(failed.OrderNumber);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentPayload_ThrowsAndDoesNotCharge()
    {
        GatewaySucceeds();
        await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        Gateway.Invocations.Clear();

        await Should.ThrowAsync<IdempotencyKeyReuseException>(() =>
            Sut.SubmitNewOrderAsync(Command(101m, key: SharedKey), default));

        Repository.All.Count.ShouldBe(1);
        VerifyChargeCalls(Times.Never);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentUsers_CreatesIndependentOrders()
    {
        GatewaySucceeds();

        var a = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        var b = await Sut.SubmitNewOrderAsync(Command(key: SharedKey) with { UserId = "other" }, default);

        b.OrderNumber.ShouldNotBe(a.OrderNumber);
        b.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        Repository.All.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SubmitNewOrder_ConcurrentSameKey_ChargesGatewayOnce()
    {
        GatewaySucceedsSlowly();

        var results = await RunConcurrently(() => Sut.SubmitNewOrderAsync(Command(key: SharedKey), default));

        VerifyChargeCalls(Times.Once);
        ShouldHaveOnePaidAndRestAlreadyPaid(results);
        results.Select(r => r.OrderNumber).Distinct().Count().ShouldBe(1);
        Repository.All.Count.ShouldBe(1);
    }
}
