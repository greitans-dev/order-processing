namespace OrderProcessing.Domain.Orders;

public sealed class InvalidIdempotencyKeyException(string message) : ArgumentException(message, "value");
