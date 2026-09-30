namespace OrderProcessing.Application.Orders.Results;

public sealed record OrderProcessingError(string OrderNumber, string Message);
