namespace OrderProcessing.Application.Orders.Payments;

public sealed record OrderProcessingError(string OrderNumber, string Message);
