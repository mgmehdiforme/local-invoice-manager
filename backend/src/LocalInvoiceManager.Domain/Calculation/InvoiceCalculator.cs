namespace LocalInvoiceManager.Domain.Calculation;

public sealed record CalcLine(decimal Quantity, decimal UnitPrice, DiscountType DiscountType, decimal DiscountValue, bool IsTaxable);

public sealed record CalcLineResult(decimal Gross, decimal DiscountAmount, decimal LineTotal);

public sealed record CalcResult(
    IReadOnlyList<CalcLineResult> Lines,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal Total);

/// <summary>
/// The single source of truth for document totals. Rules are documented in docs/DATABASE.md ("Calculation rules").
/// </summary>
public static class InvoiceCalculator
{
    public const int MaxQuantityDecimals = 3;

    public static CalcResult Calculate(
        IReadOnlyList<CalcLine> lines,
        DiscountType discountType,
        decimal discountValue,
        decimal taxRate,
        int decimals)
    {
        Validate(lines, discountType, discountValue, taxRate);

        var results = new List<CalcLineResult>(lines.Count);
        decimal subtotal = 0, taxableLines = 0;
        foreach (var line in lines)
        {
            var gross = line.Quantity * line.UnitPrice;
            var lineDiscount = Money.Discount(line.DiscountType, line.DiscountValue, Money.Round(gross, decimals), decimals);
            var lineTotal = Money.Round(gross - lineDiscount, decimals);
            if (lineTotal < 0) lineTotal = 0;
            results.Add(new CalcLineResult(Money.Round(gross, decimals), lineDiscount, lineTotal));
            subtotal += lineTotal;
            if (line.IsTaxable) taxableLines += lineTotal;
        }

        var invoiceDiscount = Money.Discount(discountType, discountValue, subtotal, decimals);

        // Spread the invoice discount over lines pro rata, so only the taxable share reduces the tax base.
        var taxableDiscount = subtotal == 0 ? 0 : Money.Round(invoiceDiscount * taxableLines / subtotal, decimals);
        var taxable = Math.Max(0, taxableLines - taxableDiscount);
        var tax = Money.Round(taxable * taxRate / 100m, decimals);
        var total = subtotal - invoiceDiscount + tax;

        return new CalcResult(results, subtotal, invoiceDiscount, taxable, tax, total);
    }

    public static PaymentStatus PaymentStatusFor(decimal total, decimal paid) =>
        paid <= 0 ? PaymentStatus.Unpaid : paid >= total ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;

    private static void Validate(IReadOnlyList<CalcLine> lines, DiscountType discountType, decimal discountValue, decimal taxRate)
    {
        if (taxRate is < 0 or > 100) throw new DomainException("calc.tax_rate", "نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
        CheckDiscount(discountType, discountValue);
        foreach (var l in lines)
        {
            if (l.Quantity <= 0) throw new DomainException("calc.quantity", "تعداد باید بیشتر از صفر باشد.");
            if (decimal.Round(l.Quantity, MaxQuantityDecimals) != l.Quantity)
                throw new DomainException("calc.quantity_precision", "تعداد حداکثر سه رقم اعشار می‌تواند داشته باشد.");
            if (l.UnitPrice < 0) throw new DomainException("calc.unit_price", "قیمت واحد نمی‌تواند منفی باشد.");
            CheckDiscount(l.DiscountType, l.DiscountValue);
        }
    }

    private static void CheckDiscount(DiscountType type, decimal value)
    {
        if (value < 0) throw new DomainException("calc.discount", "تخفیف نمی‌تواند منفی باشد.");
        if (type == DiscountType.Percent && value > 100)
            throw new DomainException("calc.discount_percent", "درصد تخفیف باید بین ۰ تا ۱۰۰ باشد.");
    }
}
