namespace OrderProcessing.Api.Contracts.V1;

/// <summary>Receipt for a paid order.</summary>
/// <param name="OrderNumber">Server-generated order number, for example <c>ORD-1A2B3C4D5E6F7A8B</c>.</param>
/// <param name="PaidAmount">Amount that was charged.</param>
/// <param name="CurrencyCode">ISO 4217 currency code of the charge.</param>
/// <param name="PaidAtUtc">When the payment was confirmed (UTC).</param>
/// <param name="PaymentConfirmation">Confirmation code returned by the payment gateway.</param>
public sealed record OrderReceiptResponse(
    string OrderNumber, decimal PaidAmount, string CurrencyCode, DateTimeOffset PaidAtUtc, string PaymentConfirmation);

/// <summary>Returned when the payment was declined.</summary>
/// <param name="OrderNumber">Number of the order that failed. It stays valid for a resubmit.</param>
/// <param name="Message">Explanation that is safe to show to the end user.</param>
public sealed record OrderErrorResponse(string OrderNumber, string Message);

/// <summary>An order in a user's history.</summary>
/// <param name="OrderNumber">Server-generated order number.</param>
/// <param name="PayableAmount">Amount to pay.</param>
/// <param name="CurrencyCode">ISO 4217 currency code.</param>
/// <param name="PaymentGatewayId">Id of the gateway used to charge the order.</param>
/// <param name="Description">The note given at submission, if any.</param>
/// <param name="Status"><c>Pending</c>, <c>Paid</c> or <c>Failed</c>. Failed orders can be resubmitted.</param>
/// <param name="FailureReason">Why the last payment attempt failed. Only set for failed orders.</param>
/// <param name="Receipt">The receipt. Only set for paid orders.</param>
/// <param name="CreatedAtUtc">When the order was created (UTC). The history is sorted by this, newest first.</param>
public sealed record OrderSummaryResponse(
    string OrderNumber, decimal PayableAmount, string CurrencyCode, string PaymentGatewayId,
    string? Description, string Status, string? FailureReason, OrderReceiptResponse? Receipt,
    DateTimeOffset CreatedAtUtc);

/// <summary>A payment gateway that can be used to pay an order.</summary>
/// <param name="Id">Value to send as <c>paymentGatewayId</c> when submitting an order.</param>
/// <param name="Name">Display name.</param>
public sealed record PaymentGatewayResponse(string Id, string Name);

/// <summary>A currency that orders can be placed in.</summary>
/// <param name="Code">ISO 4217 code to send as <c>currencyCode</c> when submitting an order.</param>
/// <param name="Name">Display name.</param>
public sealed record CurrencyResponse(string Code, string Name);
