namespace LocalInvoiceManager.Domain.Calculation;

public static class Money
{
    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero);

    public static decimal Discount(DiscountType type, decimal value, decimal baseAmount, int decimals) => type switch
    {
        DiscountType.Fixed => Math.Min(Round(value, decimals), baseAmount),
        DiscountType.Percent => Math.Min(Round(baseAmount * value / 100m, decimals), baseAmount),
        _ => 0m,
    };
}
