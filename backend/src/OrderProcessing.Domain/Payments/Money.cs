namespace OrderProcessing.Domain.Payments;

public sealed record Money
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }

    private Money(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    public static Money Of(decimal amount, string currencyCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var code = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!SupportedCurrencies.IsSupported(code))
            throw new UnsupportedCurrencyException(currencyCode ?? string.Empty);
        return new Money(amount, code);
    }
}
