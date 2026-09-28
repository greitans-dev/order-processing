using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Abstractions;

public interface IPaymentGatewayRegistry
{
    /// <exception cref="UnknownPaymentGatewayException">No gateway is registered under the id.</exception>
    IPaymentGateway Resolve(PaymentGatewayId gatewayId);
    IReadOnlyList<PaymentGatewayInfo> ListAvailable();
}
