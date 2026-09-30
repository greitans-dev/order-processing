using Microsoft.Extensions.Logging;

namespace OrderProcessing.Application.Orders.Submit;

// Never log the order description (free text) or full payloads.
public sealed partial class SubmitOrderUseCase
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Order {OrderNumber} created for user {UserId}: {Amount} {Currency} via gateway {GatewayId}")]
    private partial void LogOrderCreated(string orderNumber, string userId, decimal amount, string currency,
        string gatewayId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Idempotent replay for user {UserId}: returning order {OrderNumber} with status {Status}")]
    private partial void LogIdempotentReplay(string userId, string orderNumber, string status);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Idempotency key {IdempotencyKey} of user {UserId} was reused with a different payload; " +
                  "it belongs to order {OrderNumber}")]
    private partial void LogIdempotencyKeyConflict(string idempotencyKey, string userId, string orderNumber);
}
