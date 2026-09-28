namespace OrderProcessing.Domain.Payments;

public sealed record PaymentGatewayId
{
    public string Value { get; }

    public PaymentGatewayId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public override string ToString() => Value;
}
