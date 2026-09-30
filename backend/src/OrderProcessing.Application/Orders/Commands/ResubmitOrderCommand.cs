using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders.Commands;

public sealed record ResubmitOrderCommand(OrderNumber OrderNumber);
