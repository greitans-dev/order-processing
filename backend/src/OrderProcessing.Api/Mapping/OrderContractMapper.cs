using OrderProcessing.Api.Contracts.V1;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Commands;
using OrderProcessing.Application.Orders.Results;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Api.Mapping;

public static class OrderContractMapper
{
    public static SubmitOrderCommand ToCommand(this SubmitOrderRequest r, IdempotencyKey idempotencyKey) =>
        new(r.UserId, r.PayableAmount, r.CurrencyCode, r.PaymentGatewayId, r.Description, idempotencyKey);

    public static OrderReceiptResponse ToResponse(this OrderReceiptDto r) =>
        new(r.OrderNumber, r.PaidAmount, r.CurrencyCode, r.PaidAtUtc, r.PaymentConfirmation);

    public static OrderErrorResponse ToResponse(this OrderProcessingError e) => new(e.OrderNumber, e.Message);

    public static OrderSummaryResponse ToResponse(this OrderSummaryDto s) =>
        new(s.OrderNumber, s.PayableAmount, s.CurrencyCode, s.PaymentGatewayId, s.Description, s.Status,
            s.FailureReason, s.Receipt?.ToResponse(), s.CreatedAtUtc);

    public static PaymentGatewayResponse ToResponse(this PaymentGatewayInfo g) => new(g.Id, g.DisplayName);

    public static CurrencyResponse ToResponse(this CurrencyInfo c) => new(c.Code, c.Name);
}
