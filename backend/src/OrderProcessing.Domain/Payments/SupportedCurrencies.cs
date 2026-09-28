namespace OrderProcessing.Domain.Payments;

public sealed record CurrencyInfo(string Code, string Name);

public static class SupportedCurrencies
{
    public static IReadOnlyList<CurrencyInfo> All { get; } = [new("EUR", "Euro")];

    public static bool IsSupported(string code) =>
        All.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
}
