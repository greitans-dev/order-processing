using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Payments;
using Shouldly;

namespace OrderProcessing.Api.Tests;

public class GatewayTimeoutTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class HangingGateway : IPaymentGateway
    {
        public string GatewayId => "hanging";
        public string DisplayName => "Hanging gateway";

        public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return PaymentResult.Success("never");
        }
    }

    [Fact]
    public async Task SubmitOrder_GatewayDoesNotAnswer_Returns504ProblemWithOrderNumberAndKeepsOrderPending()
    {
        var slow = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IPaymentGateway, HangingGateway>();
            s.AddSingleton(new PaymentProcessingOptions { GatewayTimeout = TimeSpan.FromMilliseconds(100) });
            s.AddFakeLogging();
        }));
        var client = slow.CreateClient();
        var user = $"u-{Guid.NewGuid()}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                userId = user, payableAmount = 10m, currencyCode = "EUR", paymentGatewayId = "hanging"
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(504);
        var orderNumber = body.GetProperty("orderNumber").GetString()!;
        orderNumber.ShouldStartWith("ORD-");

        var orders = await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders?userId={user}");
        orders[0].GetProperty("orderNumber").GetString().ShouldBe(orderNumber);
        orders[0].GetProperty("status").GetString().ShouldBe("Pending");
        slow.Services.GetFakeLogCollector().GetSnapshot().ShouldNotContain(l => l.Level >= LogLevel.Error);
    }
}
