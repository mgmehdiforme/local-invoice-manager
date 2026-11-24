using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calculation;

namespace Domain.Tests;

public class InvoiceCalculatorTests
{
    private static CalcLine L(decimal q, decimal p, bool taxable = true, DiscountType dt = DiscountType.None, decimal dv = 0) => new(q, p, dt, dv, taxable);

    [Fact]
    public void Simple_invoice_with_tax()
    {
        var r = InvoiceCalculator.Calculate([L(2, 1000), L(1, 500)], DiscountType.None, 0, 10, 0);
        Assert.Equal(2500, r.Subtotal);
        Assert.Equal(250, r.TaxAmount);
        Assert.Equal(2750, r.Total);
    }

    [Fact]
    public void Line_discounts_fixed_and_percent()
    {
        var r = InvoiceCalculator.Calculate(
            [L(2, 1000, dt: DiscountType.Fixed, dv: 300), L(1, 1000, dt: DiscountType.Percent, dv: 25)],
            DiscountType.None, 0, 0, 0);
        Assert.Equal(1700, r.Lines[0].LineTotal);
        Assert.Equal(750, r.Lines[1].LineTotal);
        Assert.Equal(2450, r.Total);
    }

    [Fact]
    public void Invoice_discount_reduces_tax_base_pro_rata()
    {
        // 1000 taxable + 1000 non-taxable, 10% invoice discount (200) => taxable share 100 => tax 10% of 900
        var r = InvoiceCalculator.Calculate([L(1, 1000), L(1, 1000, taxable: false)], DiscountType.Percent, 10, 10, 0);
        Assert.Equal(2000, r.Subtotal);
        Assert.Equal(200, r.DiscountAmount);
        Assert.Equal(900, r.TaxableAmount);
        Assert.Equal(90, r.TaxAmount);
        Assert.Equal(1890, r.Total);
    }

    [Fact]
    public void Fixed_discount_never_exceeds_subtotal()
    {
        var r = InvoiceCalculator.Calculate([L(1, 100)], DiscountType.Fixed, 500, 10, 0);
        Assert.Equal(100, r.DiscountAmount);
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public void Rounds_half_away_from_zero_to_currency_precision()
    {
        var r = InvoiceCalculator.Calculate([L(1, 10.005m)], DiscountType.None, 0, 0, 2);
        Assert.Equal(10.01m, r.Total);
        var r0 = InvoiceCalculator.Calculate([L(3, 0.5m)], DiscountType.None, 0, 9, 0); // 1.5 -> 2 ; tax 0.18 -> 0
        Assert.Equal(2, r0.Subtotal);
        Assert.Equal(0, r0.TaxAmount);
    }

    [Fact]
    public void Fractional_quantities_up_to_three_decimals()
    {
        var r = InvoiceCalculator.Calculate([L(1.255m, 1000)], DiscountType.None, 0, 0, 0);
        Assert.Equal(1255, r.Total);
        Assert.Throws<DomainException>(() => InvoiceCalculator.Calculate([L(1.2555m, 1000)], DiscountType.None, 0, 0, 0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    public void Rejects_invalid_quantity(decimal q, decimal p) =>
        Assert.Throws<DomainException>(() => InvoiceCalculator.Calculate([L(q, p)], DiscountType.None, 0, 0, 0));

    [Fact]
    public void Rejects_out_of_range_percentages()
    {
        Assert.Throws<DomainException>(() => InvoiceCalculator.Calculate([L(1, 1)], DiscountType.Percent, 101, 0, 0));
        Assert.Throws<DomainException>(() => InvoiceCalculator.Calculate([L(1, 1)], DiscountType.None, 0, 120, 0));
    }

    [Fact]
    public void Large_rial_amounts_stay_exact()
    {
        var r = InvoiceCalculator.Calculate([L(3, 333_333_333_333m)], DiscountType.None, 0, 10, 0);
        Assert.Equal(999_999_999_999m, r.Subtotal);
        Assert.Equal(1_099_999_999_999m, r.Total); // tax 99_999_999_999.9 -> 100_000_000_000
    }

    [Theory]
    [InlineData(1000, 0, PaymentStatus.Unpaid)]
    [InlineData(1000, 1, PaymentStatus.PartiallyPaid)]
    [InlineData(1000, 1000, PaymentStatus.Paid)]
    public void Payment_status(decimal total, decimal paid, PaymentStatus expected) =>
        Assert.Equal(expected, InvoiceCalculator.PaymentStatusFor(total, paid));
}
