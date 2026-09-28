namespace OrderProcessing.Domain.Orders;

public sealed record OrderNumber
{
    public string Value { get; }

    public OrderNumber(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public static OrderNumber New() =>
        new("ORD-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());

    public override string ToString() => Value;
}
