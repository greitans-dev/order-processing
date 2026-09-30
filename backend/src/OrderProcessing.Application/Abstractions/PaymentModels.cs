using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Abstractions;

public sealed record PaymentRequest(OrderNumber OrderNumber, Money Amount, string? Description);

/// <summary>A gateway's answer to a charge: it either approved it or declined it.</summary>
public abstract record PaymentResult
{
    private PaymentResult()
    {
    }

    public static PaymentResult Success(string confirmationCode) => new Approved(confirmationCode);

    public static PaymentResult Failure(string reason) => new Declined(reason);

    public sealed record Approved(string ConfirmationCode) : PaymentResult;

    /// <param name="Reason">Explanation that is safe to show to the end user.</param>
    public sealed record Declined(string Reason) : PaymentResult;
}

public sealed record PaymentGatewayInfo(string Id, string DisplayName);
