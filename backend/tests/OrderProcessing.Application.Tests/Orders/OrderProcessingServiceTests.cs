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
    public async Task SubmitNewOrder_ValidCommand_PassesAmountAndDescriptionToGateway()
    {
        PaymentRequest? captured = null;
        SetupCharge()
            .Callback<PaymentRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(PaymentResult.Success("C"));

        await Sut.SubmitNewOrderAsync(Command(12.34m), default);

        captured.ShouldNotBeNull();
        captured.Amount.Amount.ShouldBe(12.34m);
        captured.Description.ShouldBe("desc");
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
    public async Task ResubmitOrder_FailedOrderAndGatewayDeclines_StaysFailed()
    {
        var failed = await SeedFailedOrder();

        var result = await Sut.ResubmitOrderAsync(failed.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        Repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task ResubmitOrder_UnknownOrder_ThrowsNotFound() =>
        await Should.ThrowAsync<OrderNotFoundException>(
            () => Sut.ResubmitOrderAsync(new OrderNumber("ORD-MISSING"), default));

    [Fact]
    public async Task ResubmitOrder_PaidOrder_ReturnsExistingReceiptWithoutCharging()
    {
        GatewaySucceeds();
        var first = await Sut.SubmitNewOrderAsync(Command(), default);
        Gateway.Invocations.Clear();

        var again = await Sut.ResubmitOrderAsync(new OrderNumber(first.OrderNumber), default);

        again.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        again.Receipt.ShouldBe(first.Receipt);
        VerifyChargeCalls(Times.Never);
    }

    [Fact]
    public async Task ResubmitOrder_Concurrent_ChargesGatewayOnce()
    {
        var failed = await SeedFailedOrder();
        Gateway.Invocations.Clear();
        GatewaySucceedsSlowly();

        var results = await RunConcurrently(() => Sut.ResubmitOrderAsync(failed.OrderNumber, default));

        VerifyChargeCalls(Times.Once);
        ShouldHaveOnePaidAndRestAlreadyPaid(results);
        results.Select(r => r.Receipt!.PaymentConfirmation).Distinct().ShouldBe(["CONF-X"]);
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

    [Fact]
    public async Task GetOrdersForUser_MultipleOrders_ReturnsNewestFirstWithCreationTime()
    {
        var t = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var oldest = SeedOrder("user-1", t.AddMinutes(-10));
        var newest = SeedOrder("user-1", t);
        var middle = SeedOrder("user-1", t.AddMinutes(-5));
        SeedOrder("other", t.AddHours(1));

        var orders = await Sut.GetOrdersForUserAsync("user-1", default);

        orders.Select(o => o.OrderNumber).ShouldBe(
            [newest.OrderNumber.Value, middle.OrderNumber.Value, oldest.OrderNumber.Value]);
        orders.Select(o => o.CreatedAtUtc).ShouldBe([t, t.AddMinutes(-5), t.AddMinutes(-10)]);
    }

    [Fact]
    public async Task GetOrdersForUser_EqualTimestamps_OrdersByOrderNumber()
    {
        var t = DateTimeOffset.UtcNow;
        var a = SeedOrder("user-1", t);
        var b = SeedOrder("user-1", t);

        var orders = await Sut.GetOrdersForUserAsync("user-1", default);

        orders.Select(o => o.OrderNumber).ShouldBe(
            new[] { a.OrderNumber.Value, b.OrderNumber.Value }.Order().ToList());
    }

    [Fact]
    public async Task GetOrdersForUser_OtherUsersHaveOrders_ReturnsOnlyThatUsersOrders()
    {
        GatewaySucceeds();
        await Sut.SubmitNewOrderAsync(Command(5m), default);
        await Sut.SubmitNewOrderAsync(Command(6m) with { UserId = "other" }, default);

        var orders = await Sut.GetOrdersForUserAsync("user-1", default);

        var summary = orders.ShouldHaveSingleItem();
        summary.PayableAmount.ShouldBe(5m);
        summary.CurrencyCode.ShouldBe("EUR");
        summary.Status.ShouldBe("Paid");
    }

}
