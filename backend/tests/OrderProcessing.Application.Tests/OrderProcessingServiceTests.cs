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
        _sut = new OrderProcessingService(_repository, registry.Object, new OrderNumberLockRegistry());
    }

    private static SubmitOrderCommand Command(decimal amount = 100m, string gateway = "test-gw", string currency = "EUR") =>
        new("user-1", amount, currency, gateway, "desc");

    private void GatewaySucceeds() =>
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Success("CONF-1"));

    private void GatewayDeclines() =>
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Failure("Declined: limit"));

    private async Task<Order> SeedFailedOrder()
    {
        GatewayDeclines();
        var result = await _sut.SubmitNewOrderAsync(Command(), default);
        return (await _repository.FindByOrderNumberAsync(new OrderNumber(result.OrderNumber), default))!;
    }

    [Fact]
    public async Task Submit_success_returns_receipt_and_marks_order_paid()
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
    public async Task Submit_decline_returns_error_with_order_number_and_marks_failed()
    {
        GatewayDeclines();

        var result = await _sut.SubmitNewOrderAsync(Command(), default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        result.Error.ShouldNotBeNull().Message.ShouldBe("Declined: limit");
        result.OrderNumber.ShouldStartWith("ORD-");
        _repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task Submit_passes_amount_and_description_to_gateway()
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
    public async Task Submit_with_unknown_gateway_throws_and_persists_nothing()
    {
        await Should.ThrowAsync<UnknownPaymentGatewayException>(
            () => _sut.SubmitNewOrderAsync(Command(gateway: "ghost"), default));
        _repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_with_unsupported_currency_throws_and_persists_nothing()
    {
        await Should.ThrowAsync<UnsupportedCurrencyException>(
            () => _sut.SubmitNewOrderAsync(Command(currency: "USD"), default));
        _repository.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resubmit_of_failed_order_can_succeed_and_reuses_order_number()
    {
        var failed = await SeedFailedOrder();
        GatewaySucceeds();

        var result = await _sut.ResubmitOrderAsync(failed.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        result.OrderNumber.ShouldBe(failed.OrderNumber.Value);
        _repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task Resubmit_can_fail_again()
    {
        var failed = await SeedFailedOrder();

        var result = await _sut.ResubmitOrderAsync(failed.OrderNumber, default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Failed);
        _repository.All.Single().Status.ShouldBe(OrderStatus.Failed);
    }

    [Fact]
    public async Task Resubmit_unknown_order_throws_not_found() =>
        await Should.ThrowAsync<OrderNotFoundException>(
            () => _sut.ResubmitOrderAsync(new OrderNumber("ORD-MISSING"), default));

    [Fact]
    public async Task Resubmit_of_paid_order_returns_existing_receipt_without_charging()
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
    public async Task Concurrent_resubmits_charge_the_gateway_exactly_once()
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
    public async Task GetOrdersForUser_returns_only_that_users_orders_with_currency()
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
