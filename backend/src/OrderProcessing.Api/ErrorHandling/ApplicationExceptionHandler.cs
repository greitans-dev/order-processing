using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Api.ErrorHandling;

/// <summary>
/// Turns the exceptions the application and domain use for expected failures into problem+json answers. Anything
/// else is left to the default handler, which logs it and answers with a 500 without details.
/// </summary>
public sealed class ApplicationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var problem = ToProblem(exception);
        if (problem is null) return false;

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails? ToProblem(Exception exception) => exception switch
    {
        IdempotencyKeyReuseException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Idempotency key conflict",
            Detail = exception.Message
        },
        OrderNotFoundException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Order not found",
            Detail = exception.Message
        },
        PaymentGatewayTimeoutException timeout => new ProblemDetails
        {
            Status = StatusCodes.Status504GatewayTimeout,
            Title = "Payment gateway timeout",
            Detail = "The payment gateway did not answer in time, so the payment may or may not have gone through. "
                     + "Retry with the same Idempotency-Key, or resubmit the order.",
            Extensions = { ["orderNumber"] = timeout.OrderNumber.Value }
        },
        UnknownPaymentGatewayException =>
            Invalid(nameof(SubmitOrderRequest.PaymentGatewayId), exception.Message),
        UnsupportedCurrencyException =>
            Invalid(nameof(SubmitOrderRequest.CurrencyCode), exception.Message),
        _ => null
    };

    private static ValidationProblemDetails Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] }) { Status = StatusCodes.Status400BadRequest };
}
