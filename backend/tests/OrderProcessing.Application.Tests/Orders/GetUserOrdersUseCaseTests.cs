using Shouldly;

namespace OrderProcessing.Application.Tests.Orders;

public class GetUserOrdersUseCaseTests : OrderProcessingServiceTestBase
{
    [Fact]
    public async Task Execute_MultipleOrders_ReturnsNewestFirstWithCreationTime()
    {
        var t = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var oldest = SeedOrder("user-1", t.AddMinutes(-10));
        var newest = SeedOrder("user-1", t);
        var middle = SeedOrder("user-1", t.AddMinutes(-5));
        SeedOrder("other", t.AddHours(1));

        var orders = await GetUserOrders.ExecuteAsync("user-1", default);

        orders.Select(o => o.OrderNumber).ShouldBe(
            [newest.OrderNumber.Value, middle.OrderNumber.Value, oldest.OrderNumber.Value]);
        orders.Select(o => o.CreatedAtUtc).ShouldBe([t, t.AddMinutes(-5), t.AddMinutes(-10)]);
    }

    [Fact]
    public async Task Execute_EqualTimestamps_OrdersByOrderNumber()
    {
        var t = DateTimeOffset.UtcNow;
        var a = SeedOrder("user-1", t);
        var b = SeedOrder("user-1", t);

        var orders = await GetUserOrders.ExecuteAsync("user-1", default);

        orders.Select(o => o.OrderNumber).ShouldBe(
            new[] { a.OrderNumber.Value, b.OrderNumber.Value }.Order().ToList());
    }

    [Fact]
    public async Task Execute_OtherUsersHaveOrders_ReturnsOnlyThatUsersOrders()
    {
        GatewaySucceeds();
        await Sut.SubmitNewOrderAsync(Command(5m), default);
        await Sut.SubmitNewOrderAsync(Command(6m) with { UserId = "other" }, default);

        var orders = await GetUserOrders.ExecuteAsync("user-1", default);

        var summary = orders.ShouldHaveSingleItem();
        summary.PayableAmount.ShouldBe(5m);
        summary.CurrencyCode.ShouldBe("EUR");
        summary.Status.ShouldBe("Paid");
    }
}
