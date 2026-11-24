using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Products;

public sealed record ProductInput
{
    public string Name { get; init; } = "";
    public string? Sku { get; init; }
    public ProductKind Kind { get; init; } = ProductKind.Product;
    public string Unit { get; init; } = "عدد";
    public string? Description { get; init; }
    public decimal DefaultPrice { get; init; }
    public bool IsTaxable { get; init; } = true;
}

public sealed record ProductDto(
    int Id, string Name, string? Sku, ProductKind Kind, string Unit, string? Description, decimal DefaultPrice,
    bool IsTaxable, bool IsActive, DateTime CreatedAtUtc, decimal? CustomerPrice);

public sealed record ProductQuery : PageQuery
{
    public bool? IncludeInactive { get; init; }
    public int? CustomerId { get; init; }
}

public sealed class ProductValidator : AbstractValidator<ProductInput>
{
    public ProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام کالا یا خدمت را وارد کنید.").MaximumLength(200);
        RuleFor(x => x.Sku).MaximumLength(50);
        RuleFor(x => x.Unit).NotEmpty().WithMessage("واحد را وارد کنید.").MaximumLength(30);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0).WithMessage("قیمت نمی‌تواند منفی باشد.")
            .LessThan(1_000_000_000_000_000m).WithMessage("قیمت بیش از حد بزرگ است.");
    }
}

public sealed class ProductService(IAppDbContext db, IBusinessClock clock, IValidator<ProductInput> validator)
{
    public async Task<PagedResult<ProductDto>> ListAsync(ProductQuery q, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => q.IncludeInactive == true || p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        var prices = q.CustomerId is { } cid
            ? await db.CustomerPrices.AsNoTracking().Where(x => x.CustomerId == cid).ToDictionaryAsync(x => x.ProductId, x => x.Price, ct)
            : [];
        return products
            .Where(p => Text.Matches(q.Search, p.Name, p.Sku, p.Description))
            .Select(p => ToDto(p, prices.TryGetValue(p.Id, out var cp) ? cp : null))
            .ToPaged(q);
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Product", id);
        return ToDto(p, null);
    }

    public async Task<ProductDto> CreateAsync(ProductInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var p = new Product { CreatedAtUtc = clock.UtcNow };
        await ApplyAsync(p, input, ct);
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p, null);
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var p = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Product", id);
        await ApplyAsync(p, input, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(p, null);
    }

    /// <summary>Archived products disappear from pickers; invoices keep their own copy of the line.</summary>
    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var p = await db.Products.FindAsync([id], ct) ?? throw new NotFoundException("Product", id);
        p.IsActive = active;
        p.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Product p, ProductInput i, CancellationToken ct)
    {
        var sku = Text.Clean(i.Sku);
        if (sku is not null && await db.Products.AnyAsync(x => x.Sku == sku && x.Id != p.Id, ct))
            throw new DomainException("product.duplicate_sku", "کد کالا (SKU) تکراری است.");
        p.Name = i.Name.Trim();
        p.Sku = sku;
        p.Kind = i.Kind;
        p.Unit = i.Unit.Trim();
        p.Description = Text.Clean(i.Description);
        p.DefaultPrice = i.DefaultPrice;
        p.IsTaxable = i.IsTaxable;
        p.UpdatedAtUtc = clock.UtcNow;
    }

    private static ProductDto ToDto(Product p, decimal? customerPrice) => new(
        p.Id, p.Name, p.Sku, p.Kind, p.Unit, p.Description, p.DefaultPrice, p.IsTaxable, p.IsActive, p.CreatedAtUtc, customerPrice);
}
