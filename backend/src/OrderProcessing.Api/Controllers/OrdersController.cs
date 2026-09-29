using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Api.Mapping;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
public sealed class OrdersController(OrderProcessingService service) : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Submits a new order and attempts payment. Repeating a request with the same <c>Idempotency-Key</c> and
    /// payload never creates a second order or a second charge; it returns the original outcome.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<OrderReceiptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OrderErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        SubmitOrderRequest request,
        [FromHeader(Name = IdempotencyKeyHeader), Required, MaxLength(IdempotencyKey.MaxLength)] string idempotencyKey,
        CancellationToken ct)
    {
        IdempotencyKey key;
        try
        {
            key = new IdempotencyKey(idempotencyKey);
        }
        catch (ArgumentException ex)
        {
            return Invalid(IdempotencyKeyHeader, ex.Message);
        }

        try
        {
            return ToActionResult(await service.SubmitNewOrderAsync(request.ToCommand(key), ct));
        }
        catch (IdempotencyKeyReuseException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Idempotency key conflict");
        }
        catch (UnknownPaymentGatewayException ex)
        {
            return Invalid(nameof(SubmitOrderRequest.PaymentGatewayId), ex.Message);
        }
        catch (UnsupportedCurrencyException ex)
        {
            return Invalid(nameof(SubmitOrderRequest.CurrencyCode), ex.Message);
        }
    }

    /// <summary>Retries payment for an existing order. Paid orders return their existing receipt.</summary>
    [HttpPost("{orderNumber}/resubmit")]
    [ProducesResponseType<OrderReceiptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OrderErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resubmit(string orderNumber, CancellationToken ct)
    {
        try
        {
            return ToActionResult(await service.ResubmitOrderAsync(new OrderNumber(orderNumber), ct));
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Lists a user's orders, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryResponse>>> List(
        [FromQuery, Required] string userId, CancellationToken ct)
    {
        var orders = await service.GetOrdersForUserAsync(userId, ct);
        return Ok(orders.Select(o => o.ToResponse()).ToList());
    }

    private IActionResult ToActionResult(OrderProcessingResult result) =>
        result.Outcome == OrderProcessingOutcome.Failed
            ? UnprocessableEntity(result.Error!.ToResponse())
            : Ok(result.Receipt!.ToResponse());

    private IActionResult Invalid(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));
}
