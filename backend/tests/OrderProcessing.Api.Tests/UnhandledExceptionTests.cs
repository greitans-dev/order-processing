using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Api.Tests;

public class UnhandledExceptionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SecretMessage = "database password is hunter2";

    private sealed class ThrowingOrderRepository : IOrderRepository
    {
        public Task<Order?> FindByOrderNumberAsync(OrderNumber orderNumber, CancellationToken ct) => throw Boom();
        public Task<Order?> FindByIdempotencyKeyAsync(string userId, IdempotencyKey key, CancellationToken ct) => throw Boom();
        public Task<IReadOnlyList<Order>> FindByUserIdAsync(string userId, CancellationToken ct) => throw Boom();
        public Task AddAsync(Order order, CancellationToken ct) => throw Boom();
        public Task UpdateAsync(Order order, CancellationToken ct) => throw Boom();
        private static InvalidOperationException Boom() => new(SecretMessage);
    }

    [Fact]
    public async Task SubmitOrder_UnhandledException_ReturnsProblemJsonWithoutDetailsAndLogsError()
    {
        var throwing = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<IOrderRepository>();
            services.AddSingleton<IOrderRepository, ThrowingOrderRepository>();
            services.AddFakeLogging();
        }));
        var client = throwing.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                userId = "u1", payableAmount = 10m, currencyCode = "EUR", paymentGatewayId = "mock-alpha"
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain(SecretMessage);
        raw.ShouldNotContain("at OrderProcessing");
        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("status").GetInt32().ShouldBe(500);
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();

        var error = throwing.Services.GetFakeLogCollector().GetSnapshot().Single(l => l.Level == LogLevel.Error);
        error.Exception.ShouldNotBeNull().Message.ShouldBe(SecretMessage);
    }
}
