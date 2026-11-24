using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CustomerPrice> CustomerPrices => Set<CustomerPrice>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<RecurringInvoice> RecurringInvoices => Set<RecurringInvoice>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        // decimal and DateOnly are stored as TEXT by the SQLite provider (exact values).
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppSettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.CompanyName).HasMaxLength(200);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Mobile);
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Sku).IsUnique();
        });

        b.Entity<CustomerPrice>(e =>
        {
            e.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Invoice>(e =>
        {
            e.Ignore(x => x.Balance);
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.IssueDate);
            e.HasIndex(x => x.DueDate);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.SourceQuotationId).IsUnique();
            e.HasOne(x => x.Customer).WithMany(c => c.Invoices).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(p => p.Invoice).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Quotation>().WithMany().HasForeignKey(x => x.SourceQuotationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RecurringInvoice>().WithMany().HasForeignKey(x => x.RecurringInvoiceId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Invoice>().WithMany().HasForeignKey(x => x.DuplicatedFromId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.CustomerName).HasMaxLength(200);
        });

        b.Entity<InvoiceItem>(e =>
        {
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.InvoiceId);
            e.HasIndex(x => x.PaymentDate);
        });

        b.Entity<Quotation>(e =>
        {
            e.HasIndex(x => x.QuotationNumber).IsUnique();
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<QuotationItem>(e =>
        {
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        });

        b.Entity<RecurringInvoice>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.IsActive, x.NextRunDate });
        });

        b.Entity<NotificationLog>(e =>
        {
            e.HasIndex(x => x.DedupKey).IsUnique();
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => x.IsRead);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.CreatedAtUtc);
        });

        b.Entity<NumberSequence>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(40);
        });
    }
}
