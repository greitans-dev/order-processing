using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests.Controllers;

public class PaymentGatewaysControllerTests(WebApplicationFactory<Program> factory) : ApiTestBase(factory), IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ListGateways_Default_ReturnsBothMocks()
    {
        var items = (await Json(await Client.GetAsync("/api/v1/payment-gateways"))).EnumerateArray().ToList();
        items.Select(i => i.GetProperty("id").GetString()).ShouldBe(["mock-alpha", "mock-beta"], ignoreOrder: true);
        items.All(i => !string.IsNullOrEmpty(i.GetProperty("name").GetString())).ShouldBeTrue();
    }
}
