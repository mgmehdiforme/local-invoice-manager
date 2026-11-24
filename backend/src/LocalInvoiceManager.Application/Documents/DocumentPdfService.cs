using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;

namespace LocalInvoiceManager.Application.Documents;

/// <summary>Builds the formatted PDF model for invoices and quotations and renders it.</summary>
public sealed class DocumentPdfService(IAppDbContext db, IPdfRenderer renderer, IFileStore files, InvoiceService invoices, Quotations.QuotationService quotations)
{
    public async Task<(byte[] Pdf, string FileName)> InvoicePdfAsync(int id, string? template, CancellationToken ct)
    {
        var dto = await invoices.GetAsync(id, ct);
        var c = dto.Currency;
        var meta = new List<(string, string)>
        {
            (Loc.T("تاریخ صدور"), Fmt.Date(dto.IssueDate)),
        };
        if (dto.DueDate is { } due) meta.Add((Loc.T("سررسید"), Fmt.Date(due)));
        if (dto.SourceQuotationNumber is { } qn) meta.Add((Loc.T("پیش‌فاکتور"), qn));

        var totals = BuildTotals(dto.Subtotal, dto.DiscountType, dto.DiscountValue, dto.DiscountAmount, dto.TaxRate, dto.TaxAmount, dto.Total, dto.Seller.TaxLabel, c);
        if (dto.Status == InvoiceStatus.Issued && dto.PaidAmount > 0)
        {
            totals.Add(new PdfTotalRow(Loc.T("پرداخت‌شده"), Fmt.Money(dto.PaidAmount, c)));
            totals.Add(new PdfTotalRow(Loc.T("مانده"), Fmt.Money(dto.Balance, c), true));
        }

        var model = new DocumentPdfModel
        {
            Kind = PdfKind.Invoice,
            Rtl = Loc.IsPersian,
            Title = Loc.T("فاکتور فروش"),
            Number = dto.InvoiceNumber ?? Loc.T("پیش‌نویس"),
            Template = Pick(template ?? dto.TemplateKey),
            AccentColor = dto.Seller.AccentColor,
            Watermark = dto.Status switch { InvoiceStatus.Draft => Loc.T("پیش‌نویس"), InvoiceStatus.Cancelled => Loc.T("ابطال شده"), _ => null },
            Logo = await LogoAsync(dto.Seller.LogoFileId, ct),
            Meta = meta,
            Seller = dto.Seller,
            Customer = dto.Customer,
            Lines = dto.Items.Select(l => Line(l, c)).ToList(),
            Totals = totals,
            AmountInWords = NumberToWords.ForAmount(dto.Total, c),
            Notes = dto.Notes,
            PaymentTerms = dto.PaymentTerms,
            HasTaxExemptLines = dto.Items.Any(l => !l.IsTaxable) && dto.TaxRate > 0,
        };
        var name = dto.InvoiceNumber ?? $"draft-{dto.Id}";
        return (renderer.Render(model), $"{name}.pdf");
    }

    public async Task<(byte[] Pdf, string FileName)> QuotationPdfAsync(int id, string? template, CancellationToken ct)
    {
        var dto = await quotations.GetAsync(id, ct);
        var c = dto.Currency;
        var meta = new List<(string, string)> { (Loc.T("تاریخ"), Fmt.Date(dto.IssueDate)) };
        if (dto.ValidUntil is { } v) meta.Add((Loc.T("معتبر تا"), Fmt.Date(v)));
        var model = new DocumentPdfModel
        {
            Kind = PdfKind.Quotation,
            Rtl = Loc.IsPersian,
            Title = Loc.T("پیش‌فاکتور"),
            Number = dto.QuotationNumber,
            Template = Pick(template ?? dto.TemplateKey),
            AccentColor = dto.Seller.AccentColor,
            Watermark = dto.Status == QuotationStatus.Rejected ? Loc.T("رد شده") : null,
            Logo = await LogoAsync(dto.Seller.LogoFileId, ct),
            Meta = meta,
            Seller = dto.Seller,
            Customer = dto.Customer,
            Lines = dto.Items.Select(l => Line(l, c)).ToList(),
            Totals = BuildTotals(dto.Subtotal, dto.DiscountType, dto.DiscountValue, dto.DiscountAmount, dto.TaxRate, dto.TaxAmount, dto.Total, dto.Seller.TaxLabel, c),
            AmountInWords = NumberToWords.ForAmount(dto.Total, c),
            Notes = dto.Notes,
            PaymentTerms = dto.PaymentTerms,
            ShowBank = false,
            HasTaxExemptLines = dto.Items.Any(l => !l.IsTaxable) && dto.TaxRate > 0,
        };
        return (renderer.Render(model), $"{dto.QuotationNumber}.pdf");
    }

    public static PdfLine Line(LineDto l, CurrencyDto c) => new(
        Fmt.Fa(l.LineNo.ToString()), l.Description, l.Sku, Fmt.Quantity(l.Quantity), l.Unit, Fmt.Amount(l.UnitPrice, c),
        l.DiscountAmount > 0 ? Fmt.Amount(l.DiscountAmount, c) : "—", Fmt.Amount(l.LineTotal, c), l.IsTaxable);

    public static List<PdfTotalRow> BuildTotals(decimal subtotal, DiscountType dt, decimal dv, decimal discount, decimal taxRate, decimal tax, decimal total, string taxLabel, CurrencyDto c)
    {
        var rows = new List<PdfTotalRow> { new(Loc.T("جمع ردیف‌ها"), Fmt.Money(subtotal, c)) };
        if (discount > 0)
            rows.Add(new PdfTotalRow(dt == DiscountType.Percent ? Loc.T("تخفیف ({0})", Fmt.Percent(dv)) : Loc.T("تخفیف"), "− " + Fmt.Money(discount, c)));
        if (taxRate > 0)
            rows.Add(new PdfTotalRow($"{taxLabel} ({Fmt.Percent(taxRate)})", Fmt.Money(tax, c)));
        rows.Add(new PdfTotalRow(Loc.T("مبلغ قابل پرداخت"), Fmt.Money(total, c), true));
        return rows;
    }

    public static string Pick(string? t) => t is not null && Templates.Keys.Contains(t) ? t : "modern";

    public async Task<byte[]?> LogoAsync(int? fileId, CancellationToken ct)
    {
        if (fileId is null) return null;
        var doc = await db.DocumentFiles.FindAsync([fileId.Value], ct);
        if (doc is null) return null;
        await using var s = files.OpenRead(doc.StoragePath);
        if (s is null) return null;
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
