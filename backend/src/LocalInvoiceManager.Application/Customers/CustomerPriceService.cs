using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Customers;

public sealed record CustomerPriceDto(int ProductId, string ProductName, string Unit, decimal DefaultPrice, decimal Price);

public sealed record CustomerPriceInput(int ProductId, decimal Price);

/// <summary>Special prices for one customer. The invoice editor uses them when a product is picked.</summary>
public sealed class CustomerPriceService(IAppDbContext db, IBusinessClock clock)
{
    public async Task<IReadOnlyList<CustomerPriceDto>> GetAsync(int customerId, CancellationToken ct)
    {
        _ = await db.Customers.FindAsync([customerId], ct) ?? throw new NotFoundException("Customer", customerId);
        return await db.CustomerPrices.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Product!.Name)
            .Select(x => new CustomerPriceDto(x.ProductId, x.Product!.Name, x.Product.Unit, x.Product.DefaultPrice, x.Price))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerPriceDto>> ReplaceAsync(int customerId, IReadOnlyList<CustomerPriceInput> prices, CancellationToken ct)
    {
        _ = await db.Customers.FindAsync([customerId], ct) ?? throw new NotFoundException("Customer", customerId);
        if (prices.Any(p => p.Price < 0))
            throw new DomainException("price.negative", "قیمت نمی‌تواند منفی باشد.");
        if (prices.Select(p => p.ProductId).Distinct().Count() != prices.Count)
            throw new DomainException("price.duplicate", "برای هر کالا فقط یک قیمت ویژه ثبت کنید.");
        var ids = prices.Select(p => p.ProductId).ToList();
        if (await db.Products.CountAsync(p => ids.Contains(p.Id), ct) != ids.Count)
            throw new DomainException("price.product", "یکی از کالاها پیدا نشد.");

        var existing = await db.CustomerPrices.Where(x => x.CustomerId == customerId).ToListAsync(ct);
        db.CustomerPrices.RemoveRange(existing.Where(e => !ids.Contains(e.ProductId)));
        foreach (var p in prices)
        {
            var row = existing.FirstOrDefault(e => e.ProductId == p.ProductId);
            if (row is null) db.CustomerPrices.Add(new CustomerPrice { CustomerId = customerId, ProductId = p.ProductId, Price = p.Price, UpdatedAtUtc = clock.UtcNow });
            else { row.Price = p.Price; row.UpdatedAtUtc = clock.UtcNow; }
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(customerId, ct);
    }
}
