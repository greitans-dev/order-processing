using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Infrastructure.Payments;

/// <summary>
/// Deterministic mock: amounts of 10,000.00 or more are declined, anything lower is approved with a confirmation code
/// that starts with <paramref name="confirmationPrefix"/>. Currency is ignored, which is fine while EUR is the only
/// supported currency.
/// </summary>
public sealed class MockGateway(string gatewayId, string displayName, string confirmationPrefix) : IPaymentGateway
{
    public const decimal DeclineThreshold = 10_000.00m;

    public string GatewayId => gatewayId;
    public string DisplayName => displayName;

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct) =>
        Task.FromResult<PaymentResult>(request.Amount.Amount >= DeclineThreshold
            ? new PaymentResult.Declined("Gateway declined the payment: the amount exceeds the gateway limit.")
            : new PaymentResult.Approved(
                $"{confirmationPrefix}{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"));
}
