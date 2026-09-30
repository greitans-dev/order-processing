using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrderProcessing.Api.Tests.OpenApi;

public class IdempotencyKeyOperationTransformerTests(WebApplicationFactory<Program> factory) : ApiTestBase(factory), IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task OpenApi_SubmitOperation_DescribesIdempotencyKeyHeader()
    {
        var submit = (await OpenApiDocument()).GetProperty("paths").GetProperty("/api/v1/orders").GetProperty("post");

        var header = submit.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Idempotency-Key");
        header.GetProperty("in").GetString().ShouldBe("header");
        header.GetProperty("required").GetBoolean().ShouldBeTrue();
        header.GetProperty("schema").GetProperty("maxLength").GetInt32().ShouldBe(255);
        var description = header.GetProperty("description").GetString()!;
        description.ShouldContain("UUID");
        description.ShouldContain("255");
        var operation = submit.GetProperty("description").GetString()!;
        operation.ShouldContain("Idempotency-Key");
        operation.ShouldContain("order number");
        operation.ShouldContain("409");
    }
}
