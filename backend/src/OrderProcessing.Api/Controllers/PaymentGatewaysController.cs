using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Api.Mapping;
using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Api.Controllers;

[Route("api/v{version:apiVersion}/payment-gateways")]
public sealed class PaymentGatewaysController(IPaymentGatewayRegistry registry) : ApiControllerBase
{
    /// <summary>Lists the payment gateways currently available.</summary>
    /// <response code="200">The available gateways. Use an <c>id</c> as <c>paymentGatewayId</c> when submitting an order.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentGatewayResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PaymentGatewayResponse>> List() =>
        Ok(registry.ListAvailable().Select(g => g.ToResponse()).ToList());
}
