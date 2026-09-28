using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests;

public class OrdersApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object Order(decimal amount, string user = "user-1", string gateway = "mock-alpha",
        string currency = "EUR", string? description = "test") =>
        new { userId = user, payableAmount = amount, currencyCode = currency, paymentGatewayId = gateway, description };

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>());

    [Fact]
    public async Task Submit_valid_order_returns_200_with_receipt()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/orders", Order(99.90m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        body.GetProperty("orderNumber").GetString()!.ShouldStartWith("ORD-");
        body.GetProperty("paidAmount").GetDecimal().ShouldBe(99.90m);
        body.GetProperty("currencyCode").GetString().ShouldBe("EUR");
        body.GetProperty("paymentConfirmation").GetString()!.ShouldStartWith("ALPHA-");
        body.TryGetProperty("paidAtUtc", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_at_decline_limit_returns_422_with_order_number_and_message()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/orders", Order(10000.00m, gateway: "mock-beta"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await Json(response);
        body.GetProperty("orderNumber").GetString()!.ShouldStartWith("ORD-");
        body.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0, "mock-alpha", "EUR")]
    [InlineData(-5, "mock-alpha", "EUR")]
    [InlineData(10, "ghost", "EUR")]
    [InlineData(10, "mock-alpha", "USD")]
    [InlineData(10, "", "EUR")]
    [InlineData(10, "mock-alpha", "")]
    public async Task Submit_invalid_order_returns_400(decimal amount, string gateway, string currency)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/orders", Order(amount, gateway: gateway, currency: currency));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_with_too_long_description_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/orders", Order(10m, description: new string('x', 501)));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resubmit_unknown_order_returns_404()
    {
        var response = await _client.PostAsync("/api/v1/orders/ORD-NOPE/resubmit", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resubmit_with_blank_order_number_returns_400()
    {
        var response = await _client.PostAsync("/api/v1/orders/%20/resubmit", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resubmit_failed_order_is_declined_again_with_same_order_number()
    {
        var first = await Json(await _client.PostAsJsonAsync("/api/v1/orders", Order(20000m)));
        var number = first.GetProperty("orderNumber").GetString();

        var response = await _client.PostAsync($"/api/v1/orders/{number}/resubmit", null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("orderNumber").GetString().ShouldBe(number);
    }

    [Fact]
    public async Task Resubmit_paid_order_returns_same_receipt()
    {
        var first = await Json(await _client.PostAsJsonAsync("/api/v1/orders", Order(15m)));
        var number = first.GetProperty("orderNumber").GetString();

        var response = await _client.PostAsync($"/api/v1/orders/{number}/resubmit", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var again = await Json(response);
        again.GetProperty("paymentConfirmation").GetString()
            .ShouldBe(first.GetProperty("paymentConfirmation").GetString());
        again.GetProperty("paidAtUtc").GetDateTimeOffset()
            .ShouldBe(first.GetProperty("paidAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task List_orders_by_user_includes_submission_with_status()
    {
        var user = $"user-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/orders", Order(10m, user: user));
        await _client.PostAsJsonAsync("/api/v1/orders", Order(50000m, user: user));

        var response = await _client.GetAsync($"/api/v1/orders?userId={user}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await Json(response)).EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items.Select(i => i.GetProperty("status").GetString()).ShouldBe(["Paid", "Failed"], ignoreOrder: true);
        items.All(i => i.GetProperty("currencyCode").GetString() == "EUR").ShouldBeTrue();
    }

    [Fact]
    public async Task List_orders_without_user_returns_400() =>
        (await _client.GetAsync("/api/v1/orders")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task List_gateways_returns_both_mocks()
    {
        var items = (await Json(await _client.GetAsync("/api/v1/payment-gateways"))).EnumerateArray().ToList();
        items.Select(i => i.GetProperty("id").GetString()).ShouldBe(["mock-alpha", "mock-beta"], ignoreOrder: true);
        items.All(i => !string.IsNullOrEmpty(i.GetProperty("name").GetString())).ShouldBeTrue();
    }

    [Fact]
    public async Task List_currencies_returns_euro()
    {
        var items = (await Json(await _client.GetAsync("/api/v1/currencies"))).EnumerateArray().ToList();
        var euro = items.ShouldHaveSingleItem();
        euro.GetProperty("code").GetString().ShouldBe("EUR");
        euro.GetProperty("name").GetString().ShouldBe("Euro");
    }

    [Fact]
    public async Task OpenApi_document_and_swagger_ui_are_served()
    {
        var doc = await _client.GetAsync("/openapi/v1.json");
        doc.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doc.Content.ReadAsStringAsync()).ShouldContain("/api/v1/orders");
        (await _client.GetAsync("/swagger/index.html")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
