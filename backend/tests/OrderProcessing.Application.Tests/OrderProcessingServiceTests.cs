using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Application.Tests;

public class OrderProcessingServiceTests
{
    private readonly FakeOrderRepository _repository = new();
    private readonly FakeLogger<OrderProcessingService> _logger = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly OrderProcessingService _sut;

    public OrderProcessingServiceTests()
    {
        _gateway.SetupGet(g => g.GatewayId).Returns("test-gw");
        var registry = new Mock<IPaymentGatewayRegistry>();
        registry.Setup(r => r.Resolve(It.Is<PaymentGatewayId>(id => id.Value == "test-gw")))
            .Returns(_gateway.Object);
        registry.Setup(r => r.Resolve(It.Is<PaymentGatewayId>(id => id.Value != "test-gw")))
            .Throws<UnknownPaymentGatewayException>(() => new UnknownPaymentGatewayException("nope"));
        _sut = new OrderProcessingService(_repository, registry.Object, new OrderNumberLockRegistry(), _logger);
    }

    private static SubmitOrderCommand Command(decimal amount = 100m, string gateway = "test-gw", string currency = "EUR",
        string? key = null) =>
        new("user-1", amount, currency, gateway, "desc", new IdempotencyKey(key ?? Guid.NewGuid().ToString()));

    private void GatewaySucceeds() =>
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Success("CONF-1"));

    private void GatewayDeclines() =>
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Failure("Declined: limit"));

    private Order SeedOrder(string userId, DateTimeOffset createdAtUtc)
    {
        var order = Order.Create(userId, new IdempotencyKey(Guid.NewGuid().ToString()), createdAtUtc,
            Money.Of(10m, "EUR"), new PaymentGatewayId("test-gw"), null);
        _repository.AddAsync(order, default).GetAwaiter().GetResult();
        return order;
    }

    private async Task<Order> SeedFailedOrder()
    {
        GatewayDeclines();
        var result = await _sut.SubmitNewOrderAsync(Command(), default);
        return (await _repository.FindByOrderNumberAsync(new OrderNumber(result.OrderNumber), default))!;
    }

    [Fact]
    public async Task SubmitNewOrder_GatewaySucceeds_ReturnsReceiptAndMarksPaid()
    {
        GatewaySucceeds();

        var result = await _sut.SubmitNewOrderAsync(Command(42.5m), default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        result.Receipt.ShouldNotBeNull();
        result.Receipt.PaidAmount.ShouldBe(42.5m);
        result.Receipt.CurrencyCode.ShouldBe("EUR");
        result.Receipt.PaymentConfirmation.ShouldBe("CONF-1");
        result.Receipt.OrderNumber.ShouldBe(result.OrderNumber);
        _repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task SubmitNewOrder_GatewayDeclines_ReturnsErrorAndMarksFailed()
    {
        GatewayDeclines();

        var result = await _sut.SubmitNewOrderAsync(Command(), default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        result.Error.ShouldNotBeNull().Message.ShouldBe("Declined: limit");
        result.OrderNumber.ShouldStartWith("ORD-");
        _repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task SubmitNewOrder_ValidCommand_PassesAmountAndDescriptionToGateway()
    {
        PaymentRequest? captured = null;
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(PaymentResult.Success("C"));

        await _sut.SubmitNewOrderAsync(Command(12.34m), default);

        captured.ShouldNotBeNull();
        captured.Amount.Amount.ShouldBe(12.34m);
        captured.Description.ShouldBe("desc");
    }

    [Fact]
    public async Task SubmitNewOrder_UnknownGateway_ThrowsAndPersistsNothing()
    {
        await Should.ThrowAsync<UnknownPaymentGatewayException>(
            () => _sut.SubmitNewOrderAsync(Command(gateway: "ghost"), default));
        _repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitNewOrder_UnsupportedCurrency_ThrowsAndPersistsNothing()
    {
        await Should.ThrowAsync<UnsupportedCurrencyException>(
            () => _sut.SubmitNewOrderAsync(Command(currency: "USD"), default));
        _repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResubmitOrder_FailedOrderAndGatewaySucceeds_PaysAndReusesOrderNumber()
    {
        var failed = await SeedFailedOrder();
        GatewaySucceeds();

        var result = await _sut.ResubmitOrderAsync(failed.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        result.OrderNumber.ShouldBe(failed.OrderNumber.Value);
        _repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task ResubmitOrder_FailedOrderAndGatewayDeclines_StaysFailed()
    {
        var failed = await SeedFailedOrder();

        var result = await _sut.ResubmitOrderAsync(failed.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        _repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task ResubmitOrder_UnknownOrder_ThrowsNotFound() =>
        await Should.ThrowAsync<OrderNotFoundException>(
            () => _sut.ResubmitOrderAsync(new OrderNumber("ORD-MISSING"), default));

    [Fact]
    public async Task ResubmitOrder_PaidOrder_ReturnsExistingReceiptWithoutCharging()
    {
        GatewaySucceeds();
        var first = await _sut.SubmitNewOrderAsync(Command(), default);
        _gateway.Invocations.Clear();

        var again = await _sut.ResubmitOrderAsync(new OrderNumber(first.OrderNumber), default);

        again.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        again.Receipt.ShouldBe(first.Receipt);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResubmitOrder_Concurrent_ChargesGatewayOnce()
    {
        var failed = await SeedFailedOrder();
        _gateway.Invocations.Clear();
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return PaymentResult.Success("CONF-X");
            });

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => _sut.ResubmitOrderAsync(failed.OrderNumber, default))));

        _gateway.Verify(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        results.Count(r => r.Outcome == OrderProcessingOutcome.Paid).ShouldBe(1);
        results.Count(r => r.Outcome == OrderProcessingOutcome.AlreadyPaid).ShouldBe(4);
        results.Select(r => r.Receipt!.PaymentConfirmation).Distinct().ShouldBe(["CONF-X"]);
        _repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyTwice_CreatesOneOrderAndChargesOnce()
    {
        GatewaySucceeds();

        var first = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        var second = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);

        first.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        second.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        second.OrderNumber.ShouldBe(first.OrderNumber);
        second.Receipt.ShouldBe(first.Receipt);
        _repository.All.Count.ShouldBe(1);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayOfFailedOrder_ReturnsSameFailureWithoutCharging()
    {
        GatewayDeclines();

        var first = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        var second = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);

        second.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        second.OrderNumber.ShouldBe(first.OrderNumber);
        second.Error.ShouldBe(first.Error);
        _repository.All.Count.ShouldBe(1);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayAfterSuccessfulResubmit_ReturnsAlreadyPaid()
    {
        GatewayDeclines();
        var failed = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        GatewaySucceeds();
        await _sut.ResubmitOrderAsync(new OrderNumber(failed.OrderNumber), default);

        var replay = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);

        replay.Outcome.ShouldBe(OrderProcessingOutcome.AlreadyPaid);
        replay.OrderNumber.ShouldBe(failed.OrderNumber);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentPayload_ThrowsAndDoesNotCharge()
    {
        GatewaySucceeds();
        await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        _gateway.Invocations.Clear();

        await Should.ThrowAsync<IdempotencyKeyReuseException>(() =>
            _sut.SubmitNewOrderAsync(Command(101m, key: "k1"), default));

        _repository.All.Count.ShouldBe(1);
        _gateway.Verify(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentUsers_CreatesIndependentOrders()
    {
        GatewaySucceeds();

        var a = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        var b = await _sut.SubmitNewOrderAsync(Command(key: "k1") with { UserId = "other" }, default);

        b.OrderNumber.ShouldNotBe(a.OrderNumber);
        b.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        _repository.All.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SubmitNewOrder_ConcurrentSameKey_ChargesGatewayOnce()
    {
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return PaymentResult.Success("CONF-X");
            });

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => _sut.SubmitNewOrderAsync(Command(key: "k1"), default))));

        _gateway.Verify(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        results.Count(r => r.Outcome == OrderProcessingOutcome.Paid).ShouldBe(1);
        results.Count(r => r.Outcome == OrderProcessingOutcome.AlreadyPaid).ShouldBe(4);
        results.Select(r => r.OrderNumber).Distinct().Count().ShouldBe(1);
        _repository.All.Count.ShouldBe(1);
    }

    private IReadOnlyList<FakeLogRecord> Logs => _logger.Collector.GetSnapshot();

    [Fact]
    public async Task SubmitNewOrder_GatewaySucceeds_LogsCreationAndChargeWithOrderContext()
    {
        GatewaySucceeds();

        var result = await _sut.SubmitNewOrderAsync(Command(42.5m), default);

        var created = Logs.Single(l => l.Level == LogLevel.Information && l.Message.Contains("created"));
        created.Message.ShouldContain(result.OrderNumber);
        created.Message.ShouldContain("user-1");
        created.Message.ShouldContain("test-gw");
        var succeeded = Logs.Single(l => l.Level == LogLevel.Information && l.Message.Contains("succeeded"));
        succeeded.Message.ShouldContain(result.OrderNumber);
        succeeded.Message.ShouldContain("test-gw");
        Logs.ShouldAllBe(l => l.Level < LogLevel.Warning);
    }

    [Fact]
    public async Task SubmitNewOrder_GatewayDeclines_LogsWarningWithReason()
    {
        GatewayDeclines();

        var result = await _sut.SubmitNewOrderAsync(Command(), default);

        var declined = Logs.Single(l => l.Level == LogLevel.Warning);
        declined.Message.ShouldContain(result.OrderNumber);
        declined.Message.ShouldContain("Declined: limit");
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayedKey_LogsReplayWithoutSecondCharge()
    {
        GatewaySucceeds();
        var first = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        _logger.Collector.Clear();

        await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);

        Logs.ShouldContain(l => l.Level == LogLevel.Information && l.Message.Contains("replay")
            && l.Message.Contains(first.OrderNumber));
        Logs.ShouldNotContain(l => l.Message.Contains("created"));
        Logs.ShouldNotContain(l => l.Message.Contains("Charging"));
    }

    [Fact]
    public async Task ResubmitOrder_PaidOrder_LogsWithoutCallingGateway()
    {
        GatewaySucceeds();
        var first = await _sut.SubmitNewOrderAsync(Command(), default);
        _logger.Collector.Clear();

        await _sut.ResubmitOrderAsync(new OrderNumber(first.OrderNumber), default);

        Logs.ShouldContain(l => l.Message.Contains("Resubmit requested") && l.Message.Contains(first.OrderNumber));
        Logs.ShouldContain(l => l.Message.Contains("already paid") && l.Message.Contains(first.OrderNumber));
        Logs.ShouldNotContain(l => l.Message.Contains("Charging"));
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentPayload_LogsWarning()
    {
        GatewaySucceeds();
        var first = await _sut.SubmitNewOrderAsync(Command(key: "k1"), default);
        _logger.Collector.Clear();

        await Should.ThrowAsync<IdempotencyKeyReuseException>(() =>
            _sut.SubmitNewOrderAsync(Command(101m, key: "k1"), default));

        var warning = Logs.Single(l => l.Level == LogLevel.Warning);
        warning.Message.ShouldContain("k1");
        warning.Message.ShouldContain("user-1");
        warning.Message.ShouldContain(first.OrderNumber);
    }

    [Fact]
    public async Task SubmitNewOrder_GatewayThrows_LogsErrorRethrowsAndLeavesOrderPending()
    {
        var boom = new InvalidOperationException("gateway down");
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(boom);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.SubmitNewOrderAsync(Command(), default));

        thrown.ShouldBeSameAs(boom);
        var order = _repository.All.Single();
        order.Status.ShouldBe(OrderStatus.Pending);
        var error = Logs.Single(l => l.Level == LogLevel.Error);
        error.Exception.ShouldBeSameAs(boom);
        error.Message.ShouldContain(order.OrderNumber.Value);
        error.Message.ShouldContain("test-gw");
    }

    [Fact]
    public async Task SubmitNewOrder_DescriptionProvided_NeverLogsDescription()
    {
        GatewayDeclines();
        await _sut.SubmitNewOrderAsync(Command(key: "k1") with { Description = "secret-note" }, default);
        await _sut.SubmitNewOrderAsync(Command(key: "k1") with { Description = "secret-note" }, default);

        Logs.ShouldNotBeEmpty();
        Logs.ShouldNotContain(l => l.Message.Contains("secret-note"));
    }

    [Fact]
    public async Task GetOrdersForUser_MultipleOrders_ReturnsNewestFirstWithCreationTime()
    {
        var t = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var oldest = SeedOrder("user-1", t.AddMinutes(-10));
        var newest = SeedOrder("user-1", t);
        var middle = SeedOrder("user-1", t.AddMinutes(-5));
        SeedOrder("other", t.AddHours(1));

        var orders = await _sut.GetOrdersForUserAsync("user-1", default);

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

        var orders = await _sut.GetOrdersForUserAsync("user-1", default);

        orders.Select(o => o.OrderNumber).ShouldBe(
            new[] { a.OrderNumber.Value, b.OrderNumber.Value }.Order().ToList());
    }

    [Fact]
    public async Task GetOrdersForUser_OtherUsersHaveOrders_ReturnsOnlyThatUsersOrders()
    {
        GatewaySucceeds();
        await _sut.SubmitNewOrderAsync(Command(5m), default);
        await _sut.SubmitNewOrderAsync(Command(6m) with { UserId = "other" }, default);

        var orders = await _sut.GetOrdersForUserAsync("user-1", default);

        var summary = orders.ShouldHaveSingleItem();
        summary.PayableAmount.ShouldBe(5m);
        summary.CurrencyCode.ShouldBe("EUR");
        summary.Status.ShouldBe("Paid");
    }
}
