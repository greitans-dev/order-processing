using System.ComponentModel.DataAnnotations;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Api.Contracts.V1;

/// <summary>An order to submit and pay. The <c>Idempotency-Key</c> header is sent separately.</summary>
public sealed class SubmitOrderRequest : IValidatableObject
{
    /// <summary>Identifier of the user placing the order.</summary>
    /// <example>alice</example>
    [Required]
    public string UserId { get; init; } = "";

    /// <summary>Amount to pay, in the given currency. Must be greater than zero, with at most two decimal places.</summary>
    /// <example>49.90</example>
    [Range(0.01, double.MaxValue, ErrorMessage = "Payable amount must be greater than zero.")]
    public decimal PayableAmount { get; init; }

    /// <summary>ISO 4217 currency code. See <c>GET /currencies</c> for the supported codes (currently only <c>EUR</c>).</summary>
    /// <example>EUR</example>
    [Required]
    public string CurrencyCode { get; init; } = "";

    /// <summary>Id of the payment gateway that charges the order. See <c>GET /payment-gateways</c> for the available ids.</summary>
    /// <example>mock-alpha</example>
    [Required]
    public string PaymentGatewayId { get; init; } = "";

    /// <summary>Optional free-text note for the order, at most 500 characters.</summary>
    /// <example>Birthday gift</example>
    [MaxLength(500)]
    public string? Description { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(PayableAmount, Money.MaxDecimalPlaces) != PayableAmount)
            yield return new ValidationResult(
                $"Payable amount must have at most {Money.MaxDecimalPlaces} decimal places.",
                [nameof(PayableAmount)]);
    }
}
