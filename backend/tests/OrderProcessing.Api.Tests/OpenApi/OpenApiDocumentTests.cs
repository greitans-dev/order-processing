using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests.OpenApi;

public class OpenApiDocumentTests(WebApplicationFactory<Program> factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("/api/v1/orders", "post", new[] { "200", "400", "409", "422" })]
    [InlineData("/api/v1/orders/{orderNumber}/resubmit", "post", new[] { "200", "404", "422" })]
    [InlineData("/api/v1/orders", "get", new[] { "200", "400" })]
    [InlineData("/api/v1/payment-gateways", "get", new[] { "200" })]
    [InlineData("/api/v1/currencies", "get", new[] { "200" })]
    public async Task OpenApi_EveryOperation_DocumentsEachResponseWithDescription(string path, string method, string[] codes)
    {
        var operation = (await OpenApiDocument()).GetProperty("paths").GetProperty(path).GetProperty(method);

        operation.GetProperty("summary").GetString().ShouldNotBeNullOrWhiteSpace();
        var responses = operation.GetProperty("responses");
        foreach (var code in codes)
            responses.GetProperty(code).GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    // A nullable reference to another schema is rendered as oneOf [null, {$ref, description}].
    private static string? DescriptionOf(JsonElement schema)
    {
        if (schema.TryGetProperty("description", out var description)) return description.GetString();
        return schema.TryGetProperty("oneOf", out var oneOf)
            ? oneOf.EnumerateArray().Select(DescriptionOf).FirstOrDefault(d => d is not null)
            : null;
    }

    [Fact]
    public async Task OpenApi_Schemas_DescribeEveryProperty()
    {
        var schemas = (await OpenApiDocument()).GetProperty("components").GetProperty("schemas");

        foreach (var name in new[] { "SubmitOrderRequest", "OrderReceiptResponse", "OrderErrorResponse", "OrderSummaryResponse" })
        {
            var schema = schemas.GetProperty(name);
            schema.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
            foreach (var property in schema.GetProperty("properties").EnumerateObject())
                DescriptionOf(property.Value).ShouldNotBeNullOrWhiteSpace($"{name}.{property.Name} needs a description");
        }
    }

    [Fact]
    public async Task OpenApi_DocumentAndSwaggerUi_AreServed()
    {
        var doc = await Client.GetAsync("/openapi/v1.json");
        doc.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doc.Content.ReadAsStringAsync()).ShouldContain("/api/v1/orders");
        (await Client.GetAsync("/swagger/index.html")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
