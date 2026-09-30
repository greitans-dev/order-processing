using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Api.Mapping;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Api.Controllers;

[Route("api/v{version:apiVersion}/currencies")]
public sealed class CurrenciesController : ApiControllerBase
{
    /// <summary>Lists the supported currencies.</summary>
    /// <response code="200">The supported currencies. Use a <c>code</c> as <c>currencyCode</c> when submitting an order.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CurrencyResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CurrencyResponse>> List() =>
        Ok(SupportedCurrencies.All.Select(c => c.ToResponse()).ToList());
}
