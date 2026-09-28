using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Abstractions;

public sealed record PaymentRequest(OrderNumber OrderNumber, Money Amount, string? Description);

public sealed record PaymentResult
{
    public bool IsSuccess { get; private init; }
    public string? ConfirmationCode { get; private init; }
    public string? FailureReason { get; private init; }

    public static PaymentResult Success(string confirmationCode) =>
        new() { IsSuccess = true, ConfirmationCode = confirmationCode };

    public static PaymentResult Failure(string reason) =>
        new() { IsSuccess = false, FailureReason = reason };
}

public sealed record PaymentGatewayInfo(string Id, string DisplayName);
