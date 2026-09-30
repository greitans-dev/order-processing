using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Application.Orders.Resubmit;
using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Application.Tests.Orders.Resubmit;

public class ResubmitOrderUseCaseTests : OrderUseCaseTestBase
{
    [Fact]
    public async Task Execute_FailedOrderAndGatewaySucceeds_PaysAndReusesOrderNumber()
    {
        var failed = await SeedFailedOrder();
        GatewaySucceeds();

        var result = await Resubmit.ExecuteAsync(new ResubmitOrderCommand(failed.OrderNumber), default);

        result.Outcome.ShouldBe(OrderProcessingOutcome.Paid);
        result.OrderNumber.ShouldBe(failed.OrderNumber.Value);
        Repository.All.Single().Status.ShouldBe(OrderStatus.Paid);
    }
}
