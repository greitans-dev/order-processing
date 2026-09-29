using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Api.Mapping;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/currencies")]
public sealed class CurrenciesController : ControllerBase
{
    /// <summary>Lists the supported currencies.</summary>
    /// <response code="200">The supported currencies. Use a <c>code</c> as <c>currencyCode</c> when submitting an order.</response>
    [HttpGet]
    public ActionResult<IReadOnlyList<CurrencyResponse>> List() =>
        Ok(SupportedCurrencies.All.Select(c => c.ToResponse()).ToList());
}
