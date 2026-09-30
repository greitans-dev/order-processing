using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests.OpenApi;

public class ApiInfoDocumentTransformerTests(WebApplicationFactory<Program> factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task OpenApi_Info_IsShortAndOmitsIdempotencyOverview()
    {
        var info = (await OpenApiDocument()).GetProperty("info");

        info.GetProperty("title").GetString().ShouldBe("Order Processing API");
        var description = info.GetProperty("description").GetString()!;
        description.ShouldNotBeNullOrWhiteSpace();
        description.ToLowerInvariant().ShouldNotContain("idempoten");
    }
}
