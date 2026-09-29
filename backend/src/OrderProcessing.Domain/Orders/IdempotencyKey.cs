namespace OrderProcessing.Domain.Orders;

public sealed record IdempotencyKey
{
    public const int MaxLength = 255;

    public string Value { get; }

    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaxLength)
            throw new ArgumentException($"Idempotency key must be at most {MaxLength} characters.", nameof(value));
        if (value.Any(char.IsControl))
            throw new ArgumentException("Idempotency key must not contain control characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}
