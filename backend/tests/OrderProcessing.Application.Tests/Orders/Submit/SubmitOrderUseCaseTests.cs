using Moq;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders.Submit;

public class SubmitOrderUseCaseTests : OrderUseCaseTestBase
{
    [Fact]
    public async Task SubmitNewOrder_GatewaySucceeds_ReturnsReceiptAndMarksPaid()
    {
        GatewaySucceeds();

        var result = await SubmitOrder.ExecuteAsync(Command(42.5m), default);

        var receipt = result.ShouldBeOfType<OrderProcessingResult.Paid>().Receipt;
        receipt.PaidAmount.ShouldBe(42.5m);
        receipt.CurrencyCode.ShouldBe("EUR");
        receipt.PaymentConfirmation.ShouldBe("CONF-1");
        receipt.OrderNumber.ShouldBe(result.OrderNumber);
        Repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task SubmitNewOrder_StampsCreationAndPaymentTimesFromTheClock()
    {
        GatewaySucceeds();

        var result = await SubmitOrder.ExecuteAsync(Command(), default);

        Repository.All.Single().CreatedAtUtc.ShouldBe(Clock.Now);
        result.ShouldBeOfType<OrderProcessingResult.Paid>().Receipt.PaidAtUtc.ShouldBe(Clock.Now);
    }

    [Fact]
    public async Task SubmitNewOrder_GatewayDeclines_ReturnsErrorAndMarksFailed()
    {
        GatewayDeclines();

        var result = await SubmitOrder.ExecuteAsync(Command(), default);

        result.ShouldBeOfType<OrderProcessingResult.Failed>().Error.Message.ShouldBe("Declined: limit");
        result.OrderNumber.ShouldStartWith("ORD-");
        Repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task SubmitNewOrder_UnknownGateway_ThrowsAndPersistsNothing()
    {
        await Should.ThrowAsync<UnknownPaymentGatewayException>(
            () => SubmitOrder.ExecuteAsync(Command(gateway: "ghost"), default));
        Repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitNewOrder_UnsupportedCurrency_ThrowsAndPersistsNothing()
    {
        await Should.ThrowAsync<UnsupportedCurrencyException>(
            () => SubmitOrder.ExecuteAsync(Command(currency: "USD"), default));
        Repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyTwice_CreatesOneOrderAndChargesOnce()
    {
        GatewaySucceeds();

        var first = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);
        var second = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);

        var paid = first.ShouldBeOfType<OrderProcessingResult.Paid>();
        var alreadyPaid = second.ShouldBeOfType<OrderProcessingResult.AlreadyPaid>();
        alreadyPaid.OrderNumber.ShouldBe(first.OrderNumber);
        alreadyPaid.Receipt.ShouldBe(paid.Receipt);
        Repository.All.Count.ShouldBe(1);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayOfFailedOrder_ReturnsSameFailureWithoutCharging()
    {
        GatewayDeclines();

        var first = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);
        var second = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);

        var failed = second.ShouldBeOfType<OrderProcessingResult.Failed>();
        failed.OrderNumber.ShouldBe(first.OrderNumber);
        failed.Error.ShouldBe(first.ShouldBeOfType<OrderProcessingResult.Failed>().Error);
        Repository.All.Count.ShouldBe(1);
        VerifyChargeCalls(Times.Once);
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayAfterSuccessfulResubmit_ReturnsAlreadyPaid()
    {
        GatewayDeclines();
        var failed = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);
        GatewaySucceeds();
        await Resubmit.ExecuteAsync(new OrderNumber(failed.OrderNumber), default);

        var replay = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);

        replay.ShouldBeOfType<OrderProcessingResult.AlreadyPaid>();
        replay.OrderNumber.ShouldBe(failed.OrderNumber);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentPayload_ThrowsAndDoesNotCharge()
    {
        GatewaySucceeds();
        await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);
        Gateway.Invocations.Clear();

        await Should.ThrowAsync<IdempotencyKeyReuseException>(() =>
            SubmitOrder.ExecuteAsync(Command(101m, key: SharedKey), default));

        Repository.All.Count.ShouldBe(1);
        VerifyChargeCalls(Times.Never);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentUsers_CreatesIndependentOrders()
    {
        GatewaySucceeds();

        var a = await SubmitOrder.ExecuteAsync(Command(key: SharedKey), default);
        var b = await SubmitOrder.ExecuteAsync(Command(key: SharedKey) with { UserId = "other" }, default);

        b.OrderNumber.ShouldNotBe(a.OrderNumber);
        b.ShouldBeOfType<OrderProcessingResult.Paid>();
        Repository.All.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SubmitNewOrder_ConcurrentSameKey_ChargesGatewayOnce()
    {
        GatewaySucceedsSlowly();

        var results = await RunConcurrently(() => SubmitOrder.ExecuteAsync(Command(key: SharedKey), default));

        VerifyChargeCalls(Times.Once);
        ShouldHaveOnePaidAndRestAlreadyPaid(results);
        results.Select(r => r.OrderNumber).Distinct().Count().ShouldBe(1);
        Repository.All.Count.ShouldBe(1);
    }
}
