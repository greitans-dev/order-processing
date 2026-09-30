namespace OrderProcessing.Application.Orders;

public sealed record OrderProcessingError(string OrderNumber, string Message);
