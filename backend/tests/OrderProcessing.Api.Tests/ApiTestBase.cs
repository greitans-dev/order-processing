using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OrderProcessing.Api.Tests;

public abstract class ApiTestBase(WebApplicationFactory<Program> factory)
{
    protected HttpClient Client { get; } = factory.CreateClient();

    protected static async Task<JsonElement> Json(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>());

    protected async Task<JsonElement> OpenApiDocument() => await Json(await Client.GetAsync("/openapi/v1.json"));
}
