namespace OrderProcessing.Domain.Payments;

public sealed class UnsupportedCurrencyException(string currencyCode)
    : Exception($"Currency '{currencyCode}' is not supported.")
{
    public string CurrencyCode { get; } = currencyCode;
}
