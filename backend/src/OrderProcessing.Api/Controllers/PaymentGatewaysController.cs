using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Api.Mapping;
using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payment-gateways")]
public sealed class PaymentGatewaysController(IPaymentGatewayRegistry registry) : ControllerBase
{
    /// <summary>Lists the payment gateways currently available.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<PaymentGatewayResponse>> List() =>
        Ok(registry.ListAvailable().Select(g => g.ToResponse()).ToList());
}
