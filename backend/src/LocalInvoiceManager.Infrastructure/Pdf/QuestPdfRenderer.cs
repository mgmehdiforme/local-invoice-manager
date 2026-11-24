using System.Globalization;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Domain.Localization;
using QuestPDF;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LocalInvoiceManager.Infrastructure.Pdf;

/// <summary>
/// A4 invoice/quotation PDFs, right-to-left for Persian and left-to-right for English, with embedded Vazirmatn. Three templates share one layout:
/// modern (accent band), classic (boxed, ruled table), minimal (no fills).
/// </summary>
public sealed class QuestPdfRenderer : IPdfRenderer
{
    private const string Font = "Vazirmatn";
    private static readonly object InitLock = new();
    private static bool _initialized;

    private const string Ink = "#18211f";
    private const string Muted = "#66706c";
    private const string Line = "#d9ddd9";
    private const string Soft = "#f3f5f3";

    public QuestPdfRenderer() => Initialize();

    public static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized) return;
            Settings.License = LicenseType.Community;
            Settings.UseSystemFonts = false; // only our embedded font: identical output on every machine
            var asm = typeof(QuestPdfRenderer).Assembly;
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                FontManager.RegisterFontFromStream(stream);
            }
            _initialized = true;
        }
    }

    /// <summary>Set per render: aligns to the document's end side (left in RTL, right in LTR).</summary>
    [ThreadStatic] private static bool _rtl;

    private static IContainer End(IContainer c) => _rtl ? c.AlignLeft() : c.AlignRight();
    private static IContainer Start(IContainer c) => _rtl ? c.AlignRight() : c.AlignLeft();

    public byte[] Render(DocumentPdfModel m)
    {
        _rtl = m.Rtl;
        var accent = SafeHex(m.AccentColor);
        var tint = Mix(accent, "#ffffff", 0.90);
        var t = m.Template;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(36);
            page.MarginVertical(32);
            if (m.Rtl) page.ContentFromRightToLeft();
            else page.ContentFromLeftToRight();
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(9).FontColor(Ink).LineHeight(1.35f));

            if (m.Watermark is not null)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text(m.Watermark).FontSize(72).Bold().FontColor("#18211f14");

            page.Header().ShowOnce().Element(c => Header(c, m, accent, t));
            page.Content().PaddingTop(14).Element(c => Body(c, m, accent, tint, t));
            page.Footer().Element(c => Footer(c, m));
        }))
        .WithMetadata(new DocumentMetadata { Title = $"{m.Title} {m.Number}", Creator = "Local Invoice Manager", Producer = "Local Invoice Manager" })
        .GeneratePdf();
    }

    private static void Header(IContainer c, DocumentPdfModel m, string accent, string t)
    {
        c.Column(col =>
        {
            if (t == "modern")
                col.Item().Height(6).Background(accent);

            col.Item().PaddingTop(t == "modern" ? 14 : 0).Row(row =>
            {
                // Seller (start side = right)
                row.RelativeItem(3).Row(r =>
                {
                    if (m.Logo is { Length: > 0 })
                        r.ConstantItem(56).Height(56).Image(m.Logo).FitArea();
                    var gap = m.Logo is { Length: > 0 } ? 10 : 0;
                    (m.Rtl ? r.RelativeItem().PaddingRight(gap) : r.RelativeItem().PaddingLeft(gap)).Column(s =>
                    {
                        s.Item().Text(m.Seller.CompanyName is { Length: > 0 } n ? n : "—").FontSize(14).Bold();
                        if (!string.IsNullOrWhiteSpace(m.Seller.LegalName)) s.Item().Text(m.Seller.LegalName).FontColor(Muted);
                        var ids = Join("  ", Pair(Loc.T("کد اقتصادی"), m.Seller.EconomicCode), Pair(Loc.T("شناسه ملی"), m.Seller.NationalId), Pair(Loc.T("شماره ثبت"), m.Seller.RegistrationNumber));
                        if (ids.Length > 0) s.Item().Text(Fmt.Fa(ids)).FontSize(8).FontColor(Muted);
                    });
                });

                // Title + number (end side)
                End(row.RelativeItem(2)).Column(s =>
                {
                    End(s.Item()).Text(m.Title).FontSize(18).Bold().FontColor(t == "minimal" ? Ink : accent);
                    End(s.Item()).Text(text =>
                    {
                        text.Span(Loc.T("شماره: ")).FontColor(Muted);
                        text.Span(m.Number).SemiBold().DirectionFromLeftToRight();
                    });
                    foreach (var (label, value) in m.Meta)
                        End(s.Item()).Text(text =>
                        {
                            text.Span(label + ": ").FontColor(Muted);
                            text.Span(value).SemiBold();
                        });
                });
            });

            col.Item().PaddingTop(12).LineHorizontal(t == "classic" ? 1 : 0.6f).LineColor(t == "classic" ? Ink : Line);
        });
    }

    private static void Body(IContainer c, DocumentPdfModel m, string accent, string tint, string t)
    {
        c.Column(col =>
        {
            col.Spacing(14);

            // Parties
            col.Item().Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(x => Party(x, Loc.T("فروشنده"), SellerLines(m.Seller), t, tint));
                row.RelativeItem().Element(x => Party(x, Loc.T("خریدار"), CustomerLines(m.Customer), t, tint));
            });

            // Lines
            col.Item().Element(x => LinesTable(x, m, accent, tint, t));

            // Totals + words
            col.Item().Row(row =>
            {
                (m.Rtl ? row.RelativeItem(1).PaddingLeft(16) : row.RelativeItem(1).PaddingRight(16)).Column(left =>
                {
                    if (m.AmountInWords is not null)
                    {
                        left.Item().Text(Loc.T("مبلغ به حروف")).FontSize(8).FontColor(Muted);
                        left.Item().Text(m.AmountInWords).SemiBold();
                    }
                    if (m.HasTaxExemptLines)
                        left.Item().PaddingTop(6).Text(Loc.T("ردیف‌های علامت‌دار (*) معاف از مالیات هستند.")).FontSize(8).FontColor(Muted);
                });
                row.ConstantItem(230).Element(x => Totals(x, m, accent, tint, t));
            });

            // Bank + notes
            var bank = BankLines(m.Seller);
            if (m.ShowBank && bank.Count > 0)
                col.Item().Element(x => SegBox(x, Loc.T("اطلاعات پرداخت"), bank, t));
            if (!string.IsNullOrWhiteSpace(m.PaymentTerms))
                col.Item().Element(x => Box(x, Loc.T("شرایط پرداخت"), [m.PaymentTerms!], t, tint));
            if (!string.IsNullOrWhiteSpace(m.Notes))
                col.Item().Element(x => Box(x, Loc.T("توضیحات"), [m.Notes!], t, tint));
        });
    }

    private static void Party(IContainer c, string title, List<List<Seg>> lines, string t, string tint)
    {
        var box = t switch
        {
            "modern" => c.Background(tint).Padding(10),
            "classic" => c.Border(0.8f).BorderColor(Ink).Padding(10),
            _ => c.BorderTop(0.6f).BorderColor(Line).PaddingTop(8),
        };
        box.Column(col =>
        {
            col.Item().Text(title).FontSize(8).FontColor(Muted);
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var first = i == 0;
                col.Item().Text(text =>
                {
                    if (first) text.DefaultTextStyle(x => x.FontSize(11).SemiBold());
                    WriteLine(text, line);
                });
            }
        });
    }

    private static void Box(IContainer c, string title, List<string> lines, string t, string tint)
    {
        var box = t == "classic" ? c.Border(0.6f).BorderColor(Line).Padding(8) : c.BorderTop(0.6f).BorderColor(Line).PaddingTop(6);
        box.Column(col =>
        {
            col.Item().Text(title).FontSize(8).FontColor(Muted);
            foreach (var l in lines) col.Item().Text(Fmt.Fa(l));
        });
    }

    private static void SegBox(IContainer c, string title, List<List<Seg>> lines, string t)
    {
        var box = t == "classic" ? c.Border(0.6f).BorderColor(Line).Padding(8) : c.BorderTop(0.6f).BorderColor(Line).PaddingTop(6);
        box.Column(col =>
        {
            col.Item().Text(title).FontSize(8).FontColor(Muted);
            foreach (var l in lines) col.Item().Text(text => WriteLine(text, l));
        });
    }

    private static void LinesTable(IContainer c, DocumentPdfModel m, string accent, string tint, string t)
    {
        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(24);   // #
                cols.RelativeColumn(5);    // description
                cols.RelativeColumn(1.3f); // qty
                cols.RelativeColumn(1.3f); // unit
                cols.RelativeColumn(2);    // unit price
                cols.RelativeColumn(1.6f); // discount
                cols.RelativeColumn(2.2f); // total
            });

            string[] headers = ["#", Loc.T("شرح کالا / خدمت"), Loc.T("تعداد"), Loc.T("واحد"), Loc.T("قیمت واحد"), Loc.T("تخفیف"), Loc.T("مبلغ")];
            table.Header(h =>
            {
                for (var i = 0; i < headers.Length; i++)
                {
                    var cell = h.Cell();
                    var styled = t switch
                    {
                        "modern" => cell.Background(accent).PaddingVertical(6).PaddingHorizontal(5),
                        "classic" => cell.Border(0.6f).BorderColor(Ink).Background(Soft).PaddingVertical(6).PaddingHorizontal(5),
                        _ => cell.BorderBottom(1).BorderColor(Ink).PaddingVertical(6).PaddingHorizontal(5),
                    };
                    var aligned = i >= 2 ? End(styled) : Start(styled);
                    aligned.Text(headers[i]).FontSize(8).SemiBold().FontColor(t == "modern" ? Colors.White : Ink);
                }
            });

            var n = 0;
            foreach (var l in m.Lines)
            {
                var zebra = t == "modern" && n++ % 2 == 1;
                IContainer Cell()
                {
                    IContainer cell = table.Cell();
                    cell = t switch
                    {
                        "classic" => cell.Border(0.6f).BorderColor(Line),
                        _ => cell.BorderBottom(0.5f).BorderColor(Line),
                    };
                    if (zebra) cell = cell.Background(Soft);
                    return cell.PaddingVertical(5).PaddingHorizontal(5).ShowEntire(); // keep each row on one page
                }

                Cell().Text(l.No).FontColor(Muted);
                Cell().Column(d =>
                {
                    d.Item().Text(l.Description + (m.HasTaxExemptLines && !l.IsTaxable ? " *" : ""));
                    if (!string.IsNullOrWhiteSpace(l.Sku)) d.Item().Text(l.Sku).FontSize(7).FontColor(Muted);
                });
                End(Cell()).Text(l.Quantity);
                End(Cell()).Text(l.Unit);
                End(Cell()).Text(l.UnitPrice);
                End(Cell()).Text(l.Discount).FontColor(l.Discount == "—" ? Muted : Ink);
                End(Cell()).Text(l.Total).SemiBold();
            }
        });
    }

    private static void Totals(IContainer c, DocumentPdfModel m, string accent, string tint, string t)
    {
        c.Column(col =>
        {
            foreach (var row in m.Totals)
            {
                var item = col.Item();
                if (row.Emphasis)
                {
                    var box = t switch
                    {
                        "modern" => item.PaddingTop(4).Background(accent).Padding(8),
                        "classic" => item.PaddingTop(4).Border(1).BorderColor(Ink).Padding(8),
                        _ => item.PaddingTop(4).BorderTop(1.2f).BorderColor(accent).PaddingVertical(8),
                    };
                    string color = t == "modern" ? "#ffffff" : Ink;
                    box.Row(r =>
                    {
                        r.RelativeItem().Text(row.Label).SemiBold().FontColor(color);
                        r.AutoItem().Text(row.Value).FontSize(12).Bold().FontColor(color);
                    });
                }
                else
                {
                    item.BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).Row(r =>
                    {
                        r.RelativeItem().Text(row.Label).FontColor(Muted);
                        r.AutoItem().Text(row.Value);
                    });
                }
            }
        });
    }

    private static void Footer(IContainer c, DocumentPdfModel m)
    {
        c.BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                if (!string.IsNullOrWhiteSpace(m.Seller.Address)) text.Span(Fmt.Fa(m.Seller.Address) + "   ");
                foreach (var v in new[] { m.Seller.Phone, m.Seller.Email, m.Seller.Website }.Where(v => !string.IsNullOrWhiteSpace(v)))
                    text.Span(Ltr(v!) + "   ");
            });
            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                text.Span(Loc.T("صفحه "));
                text.CurrentPageNumber().Format(p => Fmt.Fa(p?.ToString(CultureInfo.InvariantCulture)));
                text.Span(Loc.T(" از "));
                text.TotalPages().Format(p => Fmt.Fa(p?.ToString(CultureInfo.InvariantCulture)));
            });
        });
    }

    /// <summary>One printed line made of parts; numeric parts (phones, card, IBAN) are kept left-to-right.</summary>
    private sealed record Seg(string? Label, string Value, bool Ltr = false);

    private static Seg? S(string? label, string? value, bool ltr = false) => string.IsNullOrWhiteSpace(value) ? null : new Seg(label, value!, ltr);

    private static List<List<Seg>> SegLines(params Seg?[][] lines) =>
        lines.Select(l => l.Where(x => x is not null).Select(x => x!).ToList()).Where(l => l.Count > 0).ToList();

    private static List<List<Seg>> SellerLines(SellerSnapshot s) => SegLines(
        [S(null, s.CompanyName)], [S(null, s.Address)], [S(Loc.T("کد پستی"), s.PostalCode, true), S(Loc.T("تلفن"), s.Phone, true)], [S(null, s.Email, true)]);

    private static List<List<Seg>> CustomerLines(CustomerSnapshot c) => SegLines(
        [S(null, c.Name)], [S(null, c.CompanyName)], [S(null, Loc.IsPersian ? Join("، ", c.City, c.Address) : Join(", ", c.Address, c.City))],
        [S(Loc.T("کد پستی"), c.PostalCode, true), S(Loc.T("تلفن"), c.Mobile ?? c.Phone, true)],
        [S(Loc.T("کد اقتصادی"), c.TaxId, true), S(Loc.T("شناسه/کد ملی"), c.NationalId, true)], [S(null, c.Email, true)]);

    private static List<List<Seg>> BankLines(SellerSnapshot s) => SegLines(
        [S(null, s.BankName), S(Loc.T("به نام"), s.BankAccountHolder)], [S(Loc.T("شماره حساب"), s.BankAccountNumber, true)],
        [S(Loc.T("شماره کارت"), Group(s.BankCardNumber, 4), true)],
        [S(Loc.T("شبا"), s.BankIban is { } iban ? (char.IsDigit(iban[0]) ? "IR" + iban : iban) : null, true)]);

    /// <summary>
    /// Wraps a value in a Unicode left-to-right isolate (LRI … PDI). Persian digits are "Arabic numbers" in the bidi
    /// algorithm, and a dash does not hold them together, so "021-8877" or a card number would print reversed otherwise.
    /// </summary>
    private static string Ltr(string value) => "\u2066" + Fmt.Fa(value) + "\u2069";

    private static void WriteLine(TextDescriptor text, List<Seg> line)
    {
        for (var i = 0; i < line.Count; i++)
        {
            if (i > 0) text.Span(" — ").FontColor(Muted);
            var seg = line[i];
            if (seg.Label is not null) text.Span(seg.Label + ": ").FontColor(Muted);
            text.Span(seg.Ltr ? Ltr(seg.Value) : Fmt.Fa(seg.Value));
        }
    }

    private static string? Pair(string label, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";

    private static string Join(string sep, params string?[] values) => string.Join(sep, values.Where(v => !string.IsNullOrWhiteSpace(v)));

    private static string? Group(string? digits, int size) =>
        string.IsNullOrEmpty(digits) ? null : string.Join("-", Enumerable.Range(0, (digits.Length + size - 1) / size).Select(i => digits.Substring(i * size, Math.Min(size, digits.Length - i * size))));

    private static string SafeHex(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && hex.Skip(1).All(Uri.IsHexDigit) ? hex : "#0e6b5c";

    private static string Mix(string hex, string with, double amount)
    {
        static (int, int, int) Rgb(string h) => (Convert.ToInt32(h[1..3], 16), Convert.ToInt32(h[3..5], 16), Convert.ToInt32(h[5..7], 16));
        var (r1, g1, b1) = Rgb(hex);
        var (r2, g2, b2) = Rgb(with);
        int M(int a, int b) => (int)Math.Round(a + (b - a) * amount);
        return $"#{M(r1, r2):x2}{M(g1, g2):x2}{M(b1, b2):x2}";
    }
}
