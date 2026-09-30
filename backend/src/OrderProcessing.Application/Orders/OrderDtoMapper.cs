using OrderProcessing.Application.Orders.Listing;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders;

internal static class OrderDtoMapper
{
    public static OrderReceiptDto ToDto(Receipt r) =>
        new(r.OrderNumber.Value, r.PaidAmount.Amount, r.PaidAmount.CurrencyCode, r.PaidAtUtc, r.PaymentConfirmation);

    public static OrderSummaryDto ToSummary(Order o) =>
        new(o.OrderNumber.Value, o.PayableAmount.Amount, o.PayableAmount.CurrencyCode, o.PaymentGatewayId.Value,
            o.Description, o.Status.ToString(), o.FailureReason, o.Receipt is null ? null : ToDto(o.Receipt), o.CreatedAtUtc);
}
