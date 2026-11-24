using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LocalInvoiceManager.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<AppSettings> AppSettings { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Product> Products { get; }
    DbSet<CustomerPrice> CustomerPrices { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationItem> QuotationItems { get; }
    DbSet<RecurringInvoice> RecurringInvoices { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<DocumentFile> DocumentFiles { get; }
    DbSet<NumberSequence> NumberSequences { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
