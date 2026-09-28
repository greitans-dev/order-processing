using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Infrastructure.Payments;

public sealed class MockGatewayAlpha : IPaymentGateway
{
    public string GatewayId => "mock-alpha";
    public string DisplayName => "Mock Gateway Alpha";

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct) =>
        Task.FromResult(GatewayFailureRules.Evaluate(request.Amount, "ALPHA-"));
}
