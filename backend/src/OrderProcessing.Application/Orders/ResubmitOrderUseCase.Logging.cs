using Microsoft.Extensions.Logging;

namespace OrderProcessing.Application.Orders;

// Never log the order description (free text) or full payloads.
public sealed partial class ResubmitOrderUseCase
{
    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Resubmit requested for order {OrderNumber}")]
    private partial void LogResubmitRequested(string orderNumber);
}
