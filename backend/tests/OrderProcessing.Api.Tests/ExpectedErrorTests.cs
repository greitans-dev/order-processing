using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests;

/// <summary>Expected failures answer with problem+json and are not logged as unhandled errors.</summary>
public class ExpectedErrorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ExpectedErrorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddFakeLogging()));
        _client = _factory.CreateClient();
    }

    private Task<HttpResponseMessage> Submit(string user = "user-1", decimal amount = 10m, string gateway = "mock-alpha",
        string currency = "EUR", string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                userId = user, payableAmount = amount, currencyCode = currency, paymentGatewayId = gateway
            })
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return _client.SendAsync(request);
    }

    private static async Task<JsonElement> ProblemBody(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private void ShouldNotHaveLoggedErrors() =>
        _factory.Services.GetFakeLogCollector().GetSnapshot().ShouldNotContain(l => l.Level >= LogLevel.Error);

    [Fact]
    public async Task IdempotencyKeyReuse_Returns409Problem()
    {
        var user = $"u-{Guid.NewGuid()}";
        var key = Guid.NewGuid().ToString();
        await Submit(user, 10m, key: key);

        var response = await Submit(user, 11m, key: key);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await ProblemBody(response);
        body.GetProperty("status").GetInt32().ShouldBe(409);
        body.GetProperty("title").GetString().ShouldBe("Idempotency key conflict");
        ShouldNotHaveLoggedErrors();
    }

    [Fact]
    public async Task UnknownGateway_Returns400ValidationProblemOnGatewayField()
    {
        var response = await Submit(gateway: "ghost");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await ProblemBody(response);
        body.GetProperty("errors").GetProperty("PaymentGatewayId")[0].GetString()!.ShouldContain("ghost");
        ShouldNotHaveLoggedErrors();
    }

    [Fact]
    public async Task UnsupportedCurrency_Returns400ValidationProblemOnCurrencyField()
    {
        var response = await Submit(currency: "USD");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await ProblemBody(response);
        body.GetProperty("errors").GetProperty("CurrencyCode")[0].GetString()!.ShouldContain("USD");
        ShouldNotHaveLoggedErrors();
    }

    [Fact]
    public async Task ResubmitUnknownOrder_Returns404Problem()
    {
        var response = await _client.PostAsync("/api/v1/orders/ORD-NOPE/resubmit", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemBody(response)).GetProperty("status").GetInt32().ShouldBe(404);
        ShouldNotHaveLoggedErrors();
    }
}
