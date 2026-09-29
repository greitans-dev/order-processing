using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderProcessing.Api.OpenApi;

/// <summary>
/// The XML comment on the header parameter is not picked up by the OpenAPI generator, so the description and an
/// example are added here. The longer explanation lives in the remarks of <c>OrdersController.Submit</c>.
/// </summary>
internal sealed class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer
{
    private const string Description =
        "Client-generated key that identifies this order attempt, for example a UUID. " +
        "Required, at most 255 characters, no blank value or control characters. Reuse it only to retry the same request.";

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var parameter = operation.Parameters?.OfType<OpenApiParameter>()
            .FirstOrDefault(p => p.In == ParameterLocation.Header && p.Name == ApiHeaders.IdempotencyKey);
        if (parameter is null) return Task.CompletedTask;

        parameter.Description = Description;
        if (parameter.Schema is OpenApiSchema schema)
            schema.Examples = [JsonValue.Create("7b0c6d0e-3f0a-4a53-9c3e-2f6c1f5c9a11")];
        return Task.CompletedTask;
    }
}
