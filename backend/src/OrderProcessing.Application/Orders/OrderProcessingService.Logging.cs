using Microsoft.Extensions.Logging;

namespace OrderProcessing.Application.Orders;

// Never log the order description (free text) or full payloads.
public sealed partial class OrderProcessingService
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

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Resubmit requested for order {OrderNumber}")]
    private partial void LogResubmitRequested(string orderNumber);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information,
        Message = "Order {OrderNumber} is already paid; returning the existing receipt without charging")]
    private partial void LogAlreadyPaid(string orderNumber);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug,
        Message = "Charging order {OrderNumber} via gateway {GatewayId}")]
    private partial void LogChargeStarted(string orderNumber, string gatewayId);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information,
        Message = "Charge for order {OrderNumber} via gateway {GatewayId} succeeded in {ElapsedMs} ms")]
    private partial void LogChargeSucceeded(string orderNumber, string gatewayId, long elapsedMs);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Warning,
        Message = "Charge for order {OrderNumber} via gateway {GatewayId} was declined in {ElapsedMs} ms: {Reason}")]
    private partial void LogChargeDeclined(string orderNumber, string gatewayId, long elapsedMs, string reason);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Error,
        Message = "Gateway {GatewayId} threw while charging order {OrderNumber}; the order stays pending")]
    private partial void LogChargeThrew(Exception exception, string orderNumber, string gatewayId);
}
