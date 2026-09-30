using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Moq.Language.Flow;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders;

public abstract class OrderProcessingServiceTestBase
{
    protected const string SharedKey = "k1";
    private const int ConcurrentCalls = 5;

    private static readonly Expression<Func<IPaymentGateway, Task<PaymentResult>>> ChargeCall =
        g => g.ChargeAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>());

    protected FakeOrderRepository Repository { get; } = new();
    private readonly FakeLogCollector _logCollector = new();
    protected Mock<IPaymentGateway> Gateway { get; } = new();
    protected OrderPaymentProcessor Payments { get; }
    protected OrderProcessingService Sut { get; }
    protected GetUserOrdersUseCase GetUserOrders { get; }

    protected OrderProcessingServiceTestBase()
    {
        Gateway.SetupGet(g => g.GatewayId).Returns("test-gw");
        var registry = new Mock<IPaymentGatewayRegistry>();
        registry.Setup(r => r.Resolve(It.Is<PaymentGatewayId>(id => id.Value == "test-gw")))
            .Returns(Gateway.Object);
        registry.Setup(r => r.Resolve(It.Is<PaymentGatewayId>(id => id.Value != "test-gw")))
            .Throws<UnknownPaymentGatewayException>(() => new UnknownPaymentGatewayException("nope"));
        var locks = new OrderNumberLockRegistry();
        // Both classes log into one collector, so tests see the whole flow in order.
        Payments = new OrderPaymentProcessor(Repository, registry.Object, locks,
            new FakeLogger<OrderPaymentProcessor>(_logCollector));
        Sut = new OrderProcessingService(Repository, registry.Object, locks, Payments,
            new FakeLogger<OrderProcessingService>(_logCollector));
        GetUserOrders = new GetUserOrdersUseCase(Repository);
    }

    protected static SubmitOrderCommand Command(decimal amount = 100m, string gateway = "test-gw", string currency = "EUR",
        string? key = null) =>
        new("user-1", amount, currency, gateway, "desc", new IdempotencyKey(key ?? Guid.NewGuid().ToString()));

    protected ISetup<IPaymentGateway, Task<PaymentResult>> SetupCharge() => Gateway.Setup(ChargeCall);

    protected void VerifyChargeCalls(Func<Times> times) => Gateway.Verify(ChargeCall, times);

    protected void GatewaySucceeds() => SetupCharge().ReturnsAsync(PaymentResult.Success("CONF-1"));

    protected void GatewayDeclines() => SetupCharge().ReturnsAsync(PaymentResult.Failure("Declined: limit"));

    protected void GatewaySucceedsSlowly() =>
        SetupCharge().Returns(async () =>
        {
            await Task.Delay(100);
            return PaymentResult.Success("CONF-X");
        });

    protected static Task<T[]> RunConcurrently<T>(Func<Task<T>> action) =>
        Task.WhenAll(Enumerable.Range(0, ConcurrentCalls).Select(_ => Task.Run(action)));

    protected static void ShouldHaveOnePaidAndRestAlreadyPaid(IReadOnlyCollection<OrderProcessingResult> results)
    {
        results.Count(r => r.Outcome == OrderProcessingOutcome.Paid).ShouldBe(1);
        results.Count(r => r.Outcome == OrderProcessingOutcome.AlreadyPaid).ShouldBe(ConcurrentCalls - 1);
    }

    protected IReadOnlyList<FakeLogRecord> Logs => _logCollector.GetSnapshot();

    protected void ClearLogs() => _logCollector.Clear();

    protected Order SeedOrder(string userId, DateTimeOffset createdAtUtc, decimal amount = 10m, string? description = null)
    {
        var order = Order.Create(userId, new IdempotencyKey(Guid.NewGuid().ToString()), createdAtUtc,
            Money.Of(amount, "EUR"), new PaymentGatewayId("test-gw"), description);
        Repository.AddAsync(order, default).GetAwaiter().GetResult();
        return order;
    }

    protected async Task<Order> SeedFailedOrder()
    {
        GatewayDeclines();
        var result = await Sut.SubmitNewOrderAsync(Command(), default);
        return (await Repository.FindByOrderNumberAsync(new OrderNumber(result.OrderNumber), default))!;
    }
}
