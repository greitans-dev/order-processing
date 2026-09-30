using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests.Controllers;

public class OrdersControllerTests(WebApplicationFactory<Program> factory) : ApiTestBase(factory)
{
    private static object OrderPayload(decimal amount, string user = "user-1", string gateway = "mock-alpha",
        string currency = "EUR", string? description = "test") =>
        new { userId = user, payableAmount = amount, currencyCode = currency, paymentGatewayId = gateway, description };

    private Task<HttpResponseMessage> Submit(object order, string? key = null, bool withKey = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(order) };
        if (withKey) request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return Client.SendAsync(request);
    }

    [Fact]
    public async Task SubmitOrder_ValidOrder_Returns200WithReceipt()
    {
        var response = await Submit(OrderPayload(99.90m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        body.GetProperty("orderNumber").GetString()!.ShouldStartWith("ORD-");
        body.GetProperty("paidAmount").GetDecimal().ShouldBe(99.90m);
        body.GetProperty("currencyCode").GetString().ShouldBe("EUR");
        body.GetProperty("paymentConfirmation").GetString()!.ShouldStartWith("ALPHA-");
        body.TryGetProperty("paidAtUtc", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitOrder_AmountAtDeclineLimit_Returns422WithOrderNumberAndMessage()
    {
        var response = await Submit(OrderPayload(10000.00m, gateway: "mock-beta"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await Json(response);
        body.GetProperty("orderNumber").GetString()!.ShouldStartWith("ORD-");
        body.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0, "mock-alpha", "EUR")]
    [InlineData(-5, "mock-alpha", "EUR")]
    [InlineData(49.999, "mock-alpha", "EUR")]
    [InlineData(0.011, "mock-alpha", "EUR")]
    [InlineData(10, "ghost", "EUR")]
    [InlineData(10, "mock-alpha", "USD")]
    [InlineData(10, "", "EUR")]
    [InlineData(10, "mock-alpha", "")]
    public async Task SubmitOrder_InvalidOrder_Returns400(decimal amount, string gateway, string currency)
    {
        var response = await Submit(OrderPayload(amount, gateway: gateway, currency: currency));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitOrder_DescriptionTooLong_Returns400()
    {
        var response = await Submit(OrderPayload(10m, description: new string('x', 501)));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResubmitOrder_UnknownOrder_Returns404()
    {
        var response = await Client.PostAsync("/api/v1/orders/ORD-NOPE/resubmit", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResubmitOrder_BlankOrderNumber_Returns400()
    {
        var response = await Client.PostAsync("/api/v1/orders/%20/resubmit", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResubmitOrder_FailedOrder_Returns422WithSameOrderNumber()
    {
        var first = await Json(await Submit(OrderPayload(20000m)));
        var number = first.GetProperty("orderNumber").GetString();

        var response = await Client.PostAsync($"/api/v1/orders/{number}/resubmit", null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("orderNumber").GetString().ShouldBe(number);
    }

    [Fact]
    public async Task ResubmitOrder_PaidOrder_ReturnsSameReceipt()
    {
        var first = await Json(await Submit(OrderPayload(15m)));
        var number = first.GetProperty("orderNumber").GetString();

        var response = await Client.PostAsync($"/api/v1/orders/{number}/resubmit", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var again = await Json(response);
        again.GetProperty("paymentConfirmation").GetString()
            .ShouldBe(first.GetProperty("paymentConfirmation").GetString());
        again.GetProperty("paidAtUtc").GetDateTimeOffset()
            .ShouldBe(first.GetProperty("paidAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task ListOrders_UserWithSubmissions_IncludesStatus()
    {
        var user = $"user-{Guid.NewGuid():N}";
        await Submit(OrderPayload(10m, user: user));
        await Submit(OrderPayload(50000m, user: user));

        var response = await Client.GetAsync($"/api/v1/orders?userId={user}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await Json(response)).EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items.Select(i => i.GetProperty("status").GetString()).ShouldBe(["Paid", "Failed"], ignoreOrder: true);
        items.All(i => i.GetProperty("currencyCode").GetString() == "EUR").ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitOrder_MissingIdempotencyKey_Returns400()
    {
        var response = await Submit(OrderPayload(10m), withKey: false);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("bad\tkey")]
    public async Task SubmitOrder_InvalidIdempotencyKey_Returns400(string key) =>
        (await Submit(OrderPayload(10m), key)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task SubmitOrder_IdempotencyKeyTooLong_Returns400() =>
        (await Submit(OrderPayload(10m), new string('k', 256))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task SubmitOrder_RepeatedSameKey_ReturnsSameReceiptAndCreatesOneOrder()
    {
        var user = $"idem-{Guid.NewGuid()}";
        var key = Guid.NewGuid().ToString();

        var first = await Submit(OrderPayload(30m, user: user), key);
        var second = await Submit(OrderPayload(30m, user: user), key);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await Json(first);
        var secondBody = await Json(second);
        secondBody.GetProperty("orderNumber").GetString().ShouldBe(firstBody.GetProperty("orderNumber").GetString());
        secondBody.GetProperty("paymentConfirmation").GetString()
            .ShouldBe(firstBody.GetProperty("paymentConfirmation").GetString());
        (await Json(await Client.GetAsync($"/api/v1/orders?userId={user}"))).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task SubmitOrder_RepeatedDeclinedSameKey_Returns422WithSameOrderNumber()
    {
        var key = Guid.NewGuid().ToString();

        var first = await Json(await Submit(OrderPayload(20000m), key));
        var secondResponse = await Submit(OrderPayload(20000m), key);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(secondResponse)).GetProperty("orderNumber").GetString()
            .ShouldBe(first.GetProperty("orderNumber").GetString());
    }

    [Fact]
    public async Task SubmitOrder_SameKeyDifferentPayload_Returns409()
    {
        var user = $"idem-{Guid.NewGuid()}";
        var key = Guid.NewGuid().ToString();
        await Submit(OrderPayload(30m, user: user), key);

        var response = await Submit(OrderPayload(31m, user: user), key);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Json(await Client.GetAsync($"/api/v1/orders?userId={user}"))).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task ListOrders_MultipleOrders_ReturnsNewestFirstWithCreatedAt()
    {
        var user = $"order-{Guid.NewGuid()}";
        var submitted = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var body = await Json(await Submit(OrderPayload(10m + i, user: user)));
            submitted.Add(body.GetProperty("orderNumber").GetString()!);
            await Task.Delay(20);
        }

        var items = (await Json(await Client.GetAsync($"/api/v1/orders?userId={user}"))).EnumerateArray().ToList();

        items.Select(i => i.GetProperty("orderNumber").GetString()).ShouldBe(Enumerable.Reverse(submitted));
        var created = items.Select(i => i.GetProperty("createdAtUtc").GetDateTimeOffset()).ToList();
        created.ShouldBe(created.OrderByDescending(c => c));
    }

    [Fact]
    public async Task ListOrders_MissingUserId_Returns400() =>
        (await Client.GetAsync("/api/v1/orders")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
}
