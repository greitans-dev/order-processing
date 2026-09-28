using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Api.Contracts.V1;

public sealed class SubmitOrderRequest
{
    [Required]
    public string UserId { get; init; } = "";

    [Range(0.01, double.MaxValue, ErrorMessage = "Payable amount must be greater than zero.")]
    public decimal PayableAmount { get; init; }

    [Required]
    public string CurrencyCode { get; init; } = "";

    [Required]
    public string PaymentGatewayId { get; init; } = "";

    [MaxLength(500)]
    public string? Description { get; init; }
}
