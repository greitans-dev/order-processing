using Shouldly;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Tests.Orders;

public class ResubmitOrderUseCaseLoggingTests : OrderUseCaseTestBase
{
    [Fact]
    public async Task Execute_PaidOrder_LogsWithoutCallingGateway()
    {
        GatewaySucceeds();
        var first = await SubmitOrder.ExecuteAsync(Command(), default);
        ClearLogs();

        await Resubmit.ExecuteAsync(new OrderNumber(first.OrderNumber), default);

        Logs.ShouldContain(l => l.Message.Contains("Resubmit requested") && l.Message.Contains(first.OrderNumber));
        Logs.ShouldContain(l => l.Message.Contains("already paid") && l.Message.Contains(first.OrderNumber));
        Logs.ShouldNotContain(l => l.Message.Contains("Charging"));
    }
}
