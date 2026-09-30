namespace OrderProcessing.Application.Orders.Payments;

public sealed class PaymentProcessingOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// How long a single gateway charge may take before it is abandoned. Use <see cref="Timeout.InfiniteTimeSpan"/>
    /// to wait without limit.
    /// </summary>
    public TimeSpan GatewayTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
