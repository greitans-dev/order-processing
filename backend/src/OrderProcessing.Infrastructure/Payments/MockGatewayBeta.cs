using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Infrastructure.Payments;

public sealed class MockGatewayBeta : IPaymentGateway
{
    public string GatewayId => "mock-beta";
    public string DisplayName => "Mock Gateway Beta";

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct) =>
        Task.FromResult(GatewayFailureRules.Evaluate(request.Amount, "BETA-"));
}
