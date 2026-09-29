using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderProcessing.Api.OpenApi;

internal sealed class ApiInfoDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Order Processing API";
        document.Info.Description = "Submit orders for payment through a pluggable payment gateway and review order history.";
        return Task.CompletedTask;
    }
}
