using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests.Controllers;

public class CurrenciesControllerTests(WebApplicationFactory<Program> factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task ListCurrencies_Default_ReturnsEuro()
    {
        var items = (await Json(await Client.GetAsync("/api/v1/currencies"))).EnumerateArray().ToList();
        var euro = items.ShouldHaveSingleItem();
        euro.GetProperty("code").GetString().ShouldBe("EUR");
        euro.GetProperty("name").GetString().ShouldBe("Euro");
    }
}
