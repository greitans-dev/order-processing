using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders.Resubmit;

public sealed record ResubmitOrderCommand(OrderNumber OrderNumber);
