using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders;

public class OrderProcessingServiceLoggingTests : OrderProcessingServiceTestBase
{
    [Fact]
    public async Task SubmitNewOrder_GatewaySucceeds_LogsCreationAndChargeWithOrderContext()
    {
        GatewaySucceeds();

        var result = await Sut.SubmitNewOrderAsync(Command(42.5m), default);

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

        var result = await Sut.SubmitNewOrderAsync(Command(), default);

        var declined = Logs.Single(l => l.Level == LogLevel.Warning);
        declined.Message.ShouldContain(result.OrderNumber);
        declined.Message.ShouldContain("Declined: limit");
    }

    [Fact]
    public async Task SubmitNewOrder_ReplayedKey_LogsReplayWithoutSecondCharge()
    {
        GatewaySucceeds();
        var first = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        Logger.Collector.Clear();

        await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);

        Logs.ShouldContain(l => l.Level == LogLevel.Information && l.Message.Contains("replay")
            && l.Message.Contains(first.OrderNumber));
        Logs.ShouldNotContain(l => l.Message.Contains("created"));
        Logs.ShouldNotContain(l => l.Message.Contains("Charging"));
    }

    [Fact]
    public async Task ResubmitOrder_PaidOrder_LogsWithoutCallingGateway()
    {
        GatewaySucceeds();
        var first = await Sut.SubmitNewOrderAsync(Command(), default);
        Logger.Collector.Clear();

        await Sut.ResubmitOrderAsync(new OrderNumber(first.OrderNumber), default);

        Logs.ShouldContain(l => l.Message.Contains("Resubmit requested") && l.Message.Contains(first.OrderNumber));
        Logs.ShouldContain(l => l.Message.Contains("already paid") && l.Message.Contains(first.OrderNumber));
        Logs.ShouldNotContain(l => l.Message.Contains("Charging"));
    }

    [Fact]
    public async Task SubmitNewOrder_SameKeyDifferentPayload_LogsWarning()
    {
        GatewaySucceeds();
        var first = await Sut.SubmitNewOrderAsync(Command(key: SharedKey), default);
        Logger.Collector.Clear();

        await Should.ThrowAsync<IdempotencyKeyReuseException>(() =>
            Sut.SubmitNewOrderAsync(Command(101m, key: SharedKey), default));

        var warning = Logs.Single(l => l.Level == LogLevel.Warning);
        warning.Message.ShouldContain(SharedKey);
        warning.Message.ShouldContain("user-1");
        warning.Message.ShouldContain(first.OrderNumber);
    }

    [Fact]
    public async Task SubmitNewOrder_GatewayThrows_LogsErrorRethrowsAndLeavesOrderPending()
    {
        var boom = new InvalidOperationException("gateway down");
        SetupCharge().ThrowsAsync(boom);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            Sut.SubmitNewOrderAsync(Command(), default));

        thrown.ShouldBeSameAs(boom);
        var order = Repository.All.Single();
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
        await Sut.SubmitNewOrderAsync(Command(key: SharedKey) with { Description = "secret-note" }, default);
        await Sut.SubmitNewOrderAsync(Command(key: SharedKey) with { Description = "secret-note" }, default);

        Logs.ShouldNotBeEmpty();
        Logs.ShouldNotContain(l => l.Message.Contains("secret-note"));
    }
}
