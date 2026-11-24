using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Customers;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Application.Notifications;
using LocalInvoiceManager.Application.Payments;
using LocalInvoiceManager.Application.Products;
using LocalInvoiceManager.Application.Quotations;
using LocalInvoiceManager.Application.Recurring;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Web.Infrastructure;

/// <summary>
/// Fills an empty database with fictional sample data so the app can be shown or tried out
/// (Persian names and Rial prices on a Persian install, English names and prices in the default currency otherwise):
/// `docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll seed-demo`.
/// Everything goes through the normal application services, so numbering, snapshots and totals are real.
/// Refuses to run when customers or invoices already exist.
/// </summary>
public static class DemoSeeder
{
    public static async Task<int> RunAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (await db.Customers.AnyAsync(ct) || await db.Invoices.AnyAsync(ct))
        {
            Console.Error.WriteLine("The database already has customers or invoices. Sample data is only added to an empty database.");
            return 1;
        }

        var today = sp.GetRequiredService<IBusinessClock>().Today;
        var rnd = new Random(1405);
        var settings = await db.AppSettings.AsNoTracking().FirstAsync(ct);
        Fa = settings.Language == "fa";
        Loc.UseLanguage(settings.Language);
        Cal = settings.CalendarSystem;
        // Prices below are written in Rial; other currencies get the same figures / 100,000 (180,000,000 IRR → 1,800).
        Scale = Fa ? 1m : 0.00001m;
        Decimals = settings.CurrencyDecimalPlaces;

        await SeedSettingsAsync(sp.GetRequiredService<SettingsService>(), ct);

        var customers = await SeedCustomersAsync(sp.GetRequiredService<CustomerService>(), ct);
        var products = await SeedProductsAsync(sp.GetRequiredService<ProductService>(), ct);

        var prices = sp.GetRequiredService<CustomerPriceService>();
        await prices.ReplaceAsync(customers[0], [new(products["hosting"].Id, P(3_800_000)), new(products["support"].Id, P(1_200_000))], ct);
        await prices.ReplaceAsync(customers[2], [new(products["photo"].Id, P(2_000_000))], ct);

        var invoices = sp.GetRequiredService<InvoiceService>();
        var payments = sp.GetRequiredService<PaymentService>();
        var created = await SeedInvoicesAsync(invoices, payments, customers, products, today, rnd, ct);

        var quotes = await SeedQuotationsAsync(sp.GetRequiredService<QuotationService>(), customers, products, today, ct);

        await SeedRecurringAsync(sp.GetRequiredService<RecurringInvoiceService>(), customers, products, today, ct);

        await sp.GetRequiredService<NotificationService>().RunRemindersAsync(ct);

        Console.WriteLine($"Sample data added: {customers.Count} customers, {products.Count} products, {created} invoices, {quotes} quotations, 2 recurring schedules.");
        return 0;
    }

    private static bool Fa;
    private static CalendarSystem Cal = CalendarSystem.Gregorian;
    private static decimal Scale = 1m;
    private static int Decimals;

    /// <summary>A Rial price converted to the install's currency scale.</summary>
    private static decimal P(decimal rial) => Math.Round(rial * Scale, Decimals);

    private static string L(string fa, string en) => Fa ? fa : en;

    private static async Task SeedSettingsAsync(SettingsService settings, CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        if (!string.IsNullOrWhiteSpace(s.CompanyName)) return;
        if (!Fa)
        {
            await settings.UpdateAsync(s with
            {
                CompanyName = "Northwind Design Studio (sample)",
                LegalName = "Northwind Design Ltd",
                RegistrationNumber = "12345678",
                Phone = "+44 20 7946 0000",
                Email = "hello@example.com",
                Website = "example.com",
                Address = "12 Sample Street, London",
                PostalCode = "EC1A 1AA",
                BankName = "Sample Bank",
                BankAccountNumber = "12345678",
                BankIban = "GB82WEST12345698765432",
                BankAccountHolder = "Northwind Design Ltd",
                DefaultTaxRate = 20,
                TaxLabel = "VAT",
                DefaultDueDays = 15,
                DefaultPaymentTerms = "Payment due within 15 days of the invoice date.",
                DefaultNotes = "Thank you for your business.",
            }, ct);
            return;
        }
        await settings.UpdateAsync(s with
        {
            CompanyName = "استودیو طراحی نقش‌نگار (نمونه)",
            LegalName = "شرکت نقش‌نگار پارس",
            EconomicCode = "411111111111",
            NationalId = "10100000000",
            Phone = "021-88000000",
            Email = "info@example.com",
            Website = "example.com",
            Address = "تهران، خیابان ولیعصر، کوچه نمونه، پلاک ۱۲، واحد ۳",
            PostalCode = "1500000000",
            BankName = "بانک نمونه",
            BankAccountNumber = "0100000000000",
            BankCardNumber = "6037990000000000",
            BankIban = "IR000000000000000000000000",
            BankAccountHolder = "شرکت نقش‌نگار پارس",
            DefaultTaxRate = Fa ? 10 : 20,
            TaxLabel = "مالیات بر ارزش افزوده",
            DefaultDueDays = 15,
            DefaultPaymentTerms = "پرداخت حداکثر ۱۵ روز پس از صدور فاکتور، به حساب درج‌شده.",
            DefaultNotes = L("از همکاری شما سپاسگزاریم.", "Thank you for your business."),
        }, ct);
    }

    private static async Task<List<int>> SeedCustomersAsync(CustomerService service, CancellationToken ct)
    {
        CustomerInput[] en =
        [
            new() { Name = "Alice Morgan", CompanyName = "Bookworm Online", Mobile = "+44 7700 900001", Email = "alice@example.com", City = "London", Address = "1 Fleet Street" },
            new() { Name = "Dr. Ben Carter", CompanyName = "Bright Smile Dental", Mobile = "+44 7700 900002", City = "Manchester", Address = "22 King Street" },
            new() { Name = "Carlos Rivera", CompanyName = "Cypress Restaurant", Mobile = "+34 600 000 003", City = "Madrid", Address = "Calle Mayor 5" },
            new() { Name = "Dana Kim", CompanyName = "Aria Language School", Mobile = "+1 415 555 0104", Email = "dana@example.com", City = "San Francisco", Address = "100 Market St" },
            new() { Name = "Erik Larsen", CompanyName = "Fjord Construction", Mobile = "+47 400 00 005", City = "Oslo", Address = "Karl Johans gate 9" },
            new() { Name = "Fatima Noor", Mobile = "+971 50 000 0006", City = "Dubai", Address = "Marina Walk", Notes = "Prefers WhatsApp" },
            new() { Name = "George Adams", CompanyName = "Daily Grind Café", Mobile = "+1 212 555 0107", City = "New York", Address = "42 Bleecker St" },
            new() { Name = "Hana Sato", CompanyName = "Kumo Crafts Gallery", Mobile = "+81 90 0000 0008", City = "Kyoto", Address = "Gion 3-2" },
        ];
        CustomerInput[] list = Fa ?
        [
            new() { Name = "علی رضایی", CompanyName = "فروشگاه آنلاین کتاب‌نما", Mobile = "09120000001", Email = "ali@example.com", City = "تهران", Address = "تهران، میدان انقلاب، خیابان کارگر", TaxId = "411000000001" },
            new() { Name = "مریم احمدی", CompanyName = "کلینیک دندان‌پزشکی لبخند", Mobile = "09120000002", Phone = "021-22000000", City = "تهران", Address = "تهران، سعادت‌آباد، بلوار دریا" },
            new() { Name = "رضا کریمی", CompanyName = "رستوران سنتی سرو", Mobile = "09130000003", City = "اصفهان", Address = "اصفهان، خیابان چهارباغ عباسی" },
            new() { Name = "سارا محمدی", CompanyName = "آموزشگاه زبان آوا", Mobile = "09150000004", Email = "sara@example.com", City = "مشهد", Address = "مشهد، بلوار سجاد" },
            new() { Name = "حسین نوری", CompanyName = "شرکت ساختمانی بنا سازه", Mobile = "09170000005", Phone = "071-36000000", City = "شیراز", Address = "شیراز، خیابان زند", TaxId = "411000000005" },
            new() { Name = "نگار حسینی", Mobile = "09350000006", City = "تبریز", Address = "تبریز، خیابان امام", Notes = "مشتری شخصی، ترجیح تماس با واتس‌اپ" },
            new() { Name = "امیر صادقی", CompanyName = "کافه رست دانه", Mobile = "09190000007", City = "تهران", Address = "تهران، خیابان انقلاب، کوچه نمونه" },
            new() { Name = "فاطمه جعفری", CompanyName = "گالری صنایع دستی ترمه", Mobile = "09130000008", City = "یزد", Address = "یزد، بافت تاریخی، کوچه آشتی‌کنان" },
        ] : en;
        var ids = new List<int>();
        foreach (var c in list) ids.Add((await service.CreateAsync(c, ct)).Id);
        return ids;
    }

    private static async Task<Dictionary<string, ProductDto>> SeedProductsAsync(ProductService service, CancellationToken ct)
    {
        (string Key, ProductInput Input)[] list =
        [
            ("site", new() { Name = L("طراحی وب‌سایت شرکتی", "Company website design"), Sku = "WEB-01", Kind = ProductKind.Service, Unit = L("پروژه", "project"), DefaultPrice = P(180_000_000) }),
            ("shop", new() { Name = L("طراحی فروشگاه اینترنتی", "Online store design"), Sku = "WEB-02", Kind = ProductKind.Service, Unit = L("پروژه", "project"), DefaultPrice = P(320_000_000) }),
            ("logo", new() { Name = L("طراحی لوگو و هویت بصری", "Logo and brand identity"), Sku = "BRD-01", Kind = ProductKind.Service, Unit = L("پروژه", "project"), DefaultPrice = P(65_000_000) }),
            ("hosting", new() { Name = L("میزبانی وب سالانه", "Annual web hosting"), Sku = "HST-01", Kind = ProductKind.Service, Unit = L("سال", "year"), DefaultPrice = P(4_500_000) }),
            ("seo", new() { Name = L("سئو و بهینه‌سازی ماهانه", "Monthly SEO"), Sku = "SEO-01", Kind = ProductKind.Service, Unit = L("ماه", "month"), DefaultPrice = P(25_000_000) }),
            ("support", new() { Name = L("پشتیبانی فنی", "Technical support"), Sku = "SUP-01", Kind = ProductKind.Service, Unit = L("ساعت", "hour"), DefaultPrice = P(1_500_000) }),
            ("photo", new() { Name = L("عکاسی محصول", "Product photography"), Sku = "PHO-01", Kind = ProductKind.Service, Unit = L("قطعه", "item"), DefaultPrice = P(2_500_000) }),
            ("card", new() { Name = L("چاپ کارت ویزیت", "Business card printing"), Sku = "PRN-01", Kind = ProductKind.Product, Unit = L("بسته", "pack"), DefaultPrice = P(1_800_000) }),
            ("banner", new() { Name = L("چاپ بنر", "Banner printing"), Sku = "PRN-02", Kind = ProductKind.Product, Unit = L("متر مربع", "m²"), DefaultPrice = P(950_000) }),
            ("domain", new() { Name = L("ثبت دامنه ‎.ir‎", "Domain registration (.com)"), Sku = "DOM-01", Kind = ProductKind.Service, Unit = L("سال", "year"), DefaultPrice = P(600_000), IsTaxable = false }),
        ];
        var result = new Dictionary<string, ProductDto>();
        foreach (var (key, input) in list) result[key] = await service.CreateAsync(input, ct);
        return result;
    }

    private static LineInput Line(ProductDto p, decimal qty, decimal? price = null, decimal discountPercent = 0) => new()
    {
        ProductId = p.Id,
        Description = p.Name,
        Sku = p.Sku,
        Unit = p.Unit,
        Quantity = qty,
        UnitPrice = price ?? p.DefaultPrice,
        DiscountType = discountPercent > 0 ? DiscountType.Percent : DiscountType.None,
        DiscountValue = discountPercent,
        IsTaxable = p.IsTaxable,
    };

    /// <summary>About 40 invoices over the last 12 months, in date order so numbers run in sequence.</summary>
    private static async Task<int> SeedInvoicesAsync(InvoiceService invoices, PaymentService payments, List<int> customers,
        Dictionary<string, ProductDto> p, DateOnly today, Random rnd, CancellationToken ct)
    {
        List<LineInput>[] baskets =
        [
            [Line(p["site"], 1), Line(p["hosting"], 1), Line(p["domain"], 1)],
            [Line(p["shop"], 1), Line(p["hosting"], 1)],
            [Line(p["logo"], 1), Line(p["card"], 3)],
            [Line(p["seo"], 1)],
            [Line(p["seo"], 3, discountPercent: 10)],
            [Line(p["support"], 8)],
            [Line(p["support"], 12), Line(p["hosting"], 1)],
            [Line(p["photo"], 20), Line(p["banner"], 6)],
            [Line(p["banner"], 12), Line(p["card"], 5)],
            [Line(p["logo"], 1), Line(p["site"], 1, discountPercent: 5)],
        ];

        var plan = new List<(DateOnly Date, int Customer, List<LineInput> Lines)>();
        for (var monthsAgo = 11; monthsAgo >= 0; monthsAgo--)
        {
            var monthStart = Cal.AddMonths(today, -monthsAgo);
            var count = 2 + rnd.Next(3) + (monthsAgo < 4 ? 1 : 0); // a little growth in recent months
            for (var i = 0; i < count; i++)
            {
                var date = monthStart.AddDays(-rnd.Next(0, 25));
                if (date > today) date = today;
                plan.Add((date, customers[rnd.Next(customers.Count)], baskets[rnd.Next(baskets.Length)]));
            }
        }
        plan.Sort((a, b) => a.Date.CompareTo(b.Date));

        var issued = 0;
        foreach (var (date, customer, lines) in plan)
        {
            var draft = await invoices.CreateDraftAsync(new InvoiceInput
            {
                CustomerId = customer,
                IssueDate = date,
                DueDate = date.AddDays(15),
                TaxRate = Fa ? 10 : 20,
                PaymentTerms = L("پرداخت حداکثر ۱۵ روز پس از صدور فاکتور.", "Payment due within 15 days."),
                Notes = L("از همکاری شما سپاسگزاریم.", "Thank you for your business."),
                TemplateKey = (issued % 3) switch { 0 => "modern", 1 => "classic", _ => "minimal" },
                Items = lines,
            }, ct);
            var inv = await invoices.IssueAsync(draft.Id, ct);
            issued++;

            var age = today.DayNumber - date.DayNumber;
            var roll = rnd.NextDouble();
            if (age > 60 && roll < 0.9 || age > 20 && roll < 0.6)
            {
                // Paid in full, sometimes in two parts.
                if (roll < 0.3 && inv.Total > P(10_000_000))
                {
                    var first = Math.Round(inv.Total * 0.4m, Decimals);
                    await Pay(payments, inv.Id, first, date.AddDays(2), PaymentMethod.Card, ct);
                    await Pay(payments, inv.Id, inv.Total - first, Min(date.AddDays(14), today), PaymentMethod.BankTransfer, ct);
                }
                else
                {
                    await Pay(payments, inv.Id, inv.Total, Min(date.AddDays(rnd.Next(1, 14)), today), roll < 0.5 ? PaymentMethod.BankTransfer : PaymentMethod.Card, ct);
                }
            }
            else if (roll < 0.8)
            {
                // Partly paid; some of these end up overdue.
                await Pay(payments, inv.Id, Math.Round(inv.Total * 0.5m, Decimals), Min(date.AddDays(3), today), PaymentMethod.Cash, ct);
            }
            // Otherwise left unpaid (overdue once the due date passes).
        }

        // One payment that was reversed (a bounced cheque), then paid again by transfer.
        var chequeInv = await invoices.CreateDraftAsync(new InvoiceInput
        {
            CustomerId = customers[4],
            IssueDate = today.AddDays(-40),
            DueDate = today.AddDays(-25),
            TaxRate = Fa ? 10 : 20,
            Items = [Line(p["site"], 1)],
        }, ct);
        var cheque = await invoices.IssueAsync(chequeInv.Id, ct);
        var withCheque = await payments.AddAsync(cheque.Id, new PaymentInput { Amount = cheque.Total, PaymentDate = today.AddDays(-30), Method = PaymentMethod.Cheque, Reference = L("چک ۱۲۳۴۵۶", "Cheque 123456") }, ct);
        await payments.ReverseAsync(withCheque.Payments[0].Id, L("چک برگشت خورد", "Cheque bounced"), ct);
        await Pay(payments, cheque.Id, cheque.Total, today.AddDays(-20), PaymentMethod.BankTransfer, ct);

        // One cancelled invoice.
        var wrong = await invoices.CreateDraftAsync(new InvoiceInput
        {
            CustomerId = customers[6],
            IssueDate = today.AddDays(-18),
            DueDate = today.AddDays(-3),
            TaxRate = Fa ? 10 : 20,
            Items = [Line(p["banner"], 4)],
        }, ct);
        await invoices.IssueAsync(wrong.Id, ct);
        await invoices.CancelAsync(wrong.Id, L("اندازه بنر اشتباه وارد شده بود؛ فاکتور جدید صادر شد.", "Wrong banner size; a new invoice was issued."), ct);

        // Two drafts waiting to be issued.
        await invoices.CreateDraftAsync(new InvoiceInput
        {
            CustomerId = customers[3], IssueDate = today, DueDate = today.AddDays(15), TaxRate = Fa ? 10 : 20,
            Items = [Line(p["seo"], 1), Line(p["support"], 4)],
        }, ct);
        await invoices.CreateDraftAsync(new InvoiceInput
        {
            CustomerId = customers[7], IssueDate = today, DueDate = today.AddDays(15), TaxRate = Fa ? 10 : 20,
            DiscountType = DiscountType.Fixed, DiscountValue = P(5_000_000),
            Items = [Line(p["shop"], 1), Line(p["photo"], 30, P(2_200_000))],
        }, ct);

        return issued + 4;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static Task Pay(PaymentService payments, int invoiceId, decimal amount, DateOnly date, PaymentMethod method, CancellationToken ct) =>
        payments.AddAsync(invoiceId, new PaymentInput { Amount = amount, PaymentDate = date, Method = method }, ct);

    private static async Task<int> SeedQuotationsAsync(QuotationService service, List<int> customers, Dictionary<string, ProductDto> p,
        DateOnly today, CancellationToken ct)
    {
        QuotationInput Make(int customer, int daysAgo, List<LineInput> lines) => new()
        {
            CustomerId = customer,
            IssueDate = today.AddDays(-daysAgo),
            ValidUntil = today.AddDays(-daysAgo + 30),
            TaxRate = Fa ? 10 : 20,
            PaymentTerms = L("۵۰٪ پیش‌پرداخت، ۵۰٪ پس از تحویل.", "50% upfront, 50% on delivery."),
            Items = lines,
        };

        await service.CreateAsync(Make(customers[5], 1, [Line(p["logo"], 1), Line(p["card"], 2)]), ct);

        var sent = await service.CreateAsync(Make(customers[1], 6, [Line(p["site"], 1), Line(p["seo"], 3)]), ct);
        await service.SendAsync(sent.Id, ct);

        var accepted = await service.CreateAsync(Make(customers[2], 10, [Line(p["photo"], 40, P(2_000_000)), Line(p["banner"], 8)]), ct);
        await service.SendAsync(accepted.Id, ct);
        await service.AcceptAsync(accepted.Id, ct);

        var rejected = await service.CreateAsync(Make(customers[6], 25, [Line(p["shop"], 1)]), ct);
        await service.SendAsync(rejected.Id, ct);
        await service.RejectAsync(rejected.Id, ct);

        var converted = await service.CreateAsync(Make(customers[0], 14, [Line(p["support"], 10, P(1_200_000)), Line(p["hosting"], 1, P(3_800_000))]), ct);
        await service.SendAsync(converted.Id, ct);
        await service.AcceptAsync(converted.Id, ct);
        await service.ConvertAsync(converted.Id, ct);

        return 5;
    }

    private static async Task SeedRecurringAsync(RecurringInvoiceService service, List<int> customers, Dictionary<string, ProductDto> p,
        DateOnly today, CancellationToken ct)
    {
        var nextMonthStart = Cal.AddMonths(Cal.MonthStart(today), 1);

        await service.CreateAsync(new RecurringInput
        {
            Name = L("سئو ماهانه آموزشگاه آوا", "Monthly SEO — Aria Language School"),
            CustomerId = customers[3],
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = nextMonthStart,
            DueDays = 10,
            Template = new RecurringTemplate { TaxRate = Fa ? 10 : 20, Items = [Line(p["seo"], 1)] },
        }, ct);

        await service.CreateAsync(new RecurringInput
        {
            Name = L("پشتیبانی فصلی کتاب‌نما", "Quarterly support — Bookworm Online"),
            CustomerId = customers[0],
            Frequency = RecurrenceFrequency.Quarterly,
            StartDate = nextMonthStart,
            DueDays = 15,
            Template = new RecurringTemplate { TaxRate = Fa ? 10 : 20, Items = [Line(p["support"], 10, P(1_200_000))] },
        }, ct);
    }
}
