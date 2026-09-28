namespace OrderProcessing.Application.Abstractions;

public interface IPaymentGateway
{
    string GatewayId { get; }
    string DisplayName { get; }
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);
}
