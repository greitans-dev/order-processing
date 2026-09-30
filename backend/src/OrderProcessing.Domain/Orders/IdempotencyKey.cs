namespace OrderProcessing.Domain.Orders;

public sealed record IdempotencyKey
{
    public const int MaxLength = 255;

    public string Value { get; }

    public IdempotencyKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidIdempotencyKeyException("Idempotency key must not be blank.");
        if (value.Length > MaxLength)
            throw new InvalidIdempotencyKeyException($"Idempotency key must be at most {MaxLength} characters.");
        if (value.Any(char.IsControl))
            throw new InvalidIdempotencyKeyException("Idempotency key must not contain control characters.");
        Value = value;
    }

    public override string ToString() => Value;
}
