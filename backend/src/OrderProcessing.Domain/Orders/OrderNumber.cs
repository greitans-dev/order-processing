using System.Security.Cryptography;

namespace OrderProcessing.Domain.Orders;

public sealed record OrderNumber
{
    public string Value { get; }

    public OrderNumber(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Generates an unguessable number with 64 random bits, so collisions and enumeration are impractical.</summary>
    public static OrderNumber New() => new("ORD-" + RandomNumberGenerator.GetHexString(16));

    public override string ToString() => Value;
}
