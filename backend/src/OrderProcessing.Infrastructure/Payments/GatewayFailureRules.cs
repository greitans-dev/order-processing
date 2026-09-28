using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Infrastructure.Payments;

/// <summary>
/// Deterministic decline rule shared by the mock gateways: amounts of 10,000.00 or more are declined.
/// Currency is ignored, which is fine while EUR is the only supported currency.
/// </summary>
internal static class GatewayFailureRules
{
    public const decimal DeclineThreshold = 10_000.00m;

    public static PaymentResult Evaluate(Money amount, string confirmationPrefix) =>
        amount.Amount >= DeclineThreshold
            ? PaymentResult.Failure("Gateway declined the payment: the amount exceeds the gateway limit.")
            : PaymentResult.Success($"{confirmationPrefix}{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}");
}
