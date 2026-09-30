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

    public const int MaxDecimalPlaces = 2;

    public static Money Of(decimal amount, string currencyCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (decimal.Round(amount, MaxDecimalPlaces) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), amount,
                $"Amount must have at most {MaxDecimalPlaces} decimal places.");
        var code = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!SupportedCurrencies.IsSupported(code))
            throw new UnsupportedCurrencyException(currencyCode ?? string.Empty);
        return new Money(amount, code);
    }
}
