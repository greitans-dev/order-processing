using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Infrastructure.Payments;

public sealed class PaymentGatewayRegistry : IPaymentGatewayRegistry
{
    private readonly Dictionary<string, IPaymentGateway> _gateways;

    public PaymentGatewayRegistry(IEnumerable<IPaymentGateway> gateways)
    {
        try
        {
            _gateways = gateways.ToDictionary(g => g.GatewayId, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Multiple payment gateways share the same GatewayId.", ex);
        }
    }

    public IPaymentGateway Resolve(PaymentGatewayId gatewayId) =>
        _gateways.TryGetValue(gatewayId.Value, out var gateway)
            ? gateway
            : throw new UnknownPaymentGatewayException(gatewayId.Value);

    public IReadOnlyList<PaymentGatewayInfo> ListAvailable() =>
        _gateways.Values.Select(g => new PaymentGatewayInfo(g.GatewayId, g.DisplayName)).ToList();
}
