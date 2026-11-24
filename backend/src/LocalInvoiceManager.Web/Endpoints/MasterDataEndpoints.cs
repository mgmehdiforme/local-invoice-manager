using LocalInvoiceManager.Application.Customers;
using LocalInvoiceManager.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace LocalInvoiceManager.Web.Endpoints;

public static class MasterDataEndpoints
{
    public static void MapCustomers(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/customers").WithTags("Customers");
        g.MapGet("/", ([AsParameters] CustomerQuery q, CustomerService s, CancellationToken ct) => s.ListAsync(q, ct));
        g.MapGet("/{id:int}", (int id, CustomerService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/", async (CustomerInput input, CustomerService s, CancellationToken ct) =>
        {
            var c = await s.CreateAsync(input, ct);
            return Results.Created($"/api/v1/customers/{c.Id}", c);
        });
        g.MapPut("/{id:int}", (int id, CustomerInput input, CustomerService s, CancellationToken ct) => s.UpdateAsync(id, input, ct));
        g.MapDelete("/{id:int}", async (int id, CustomerService s, CancellationToken ct) =>
        {
            await s.SetActiveAsync(id, false, ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:int}/restore", async (int id, CustomerService s, CancellationToken ct) =>
        {
            await s.SetActiveAsync(id, true, ct);
            return Results.NoContent();
        });
        g.MapGet("/{id:int}/balance", (int id, CustomerService s, CancellationToken ct) => s.GetBalanceAsync(id, ct));
    }

    public static void MapProducts(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/products").WithTags("Products");
        g.MapGet("/", ([AsParameters] ProductQuery q, ProductService s, CancellationToken ct) => s.ListAsync(q, ct));
        g.MapGet("/{id:int}", (int id, ProductService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/", async (ProductInput input, ProductService s, CancellationToken ct) =>
        {
            var p = await s.CreateAsync(input, ct);
            return Results.Created($"/api/v1/products/{p.Id}", p);
        });
        g.MapPut("/{id:int}", (int id, ProductInput input, ProductService s, CancellationToken ct) => s.UpdateAsync(id, input, ct));
        g.MapDelete("/{id:int}", async (int id, ProductService s, CancellationToken ct) =>
        {
            await s.SetActiveAsync(id, false, ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:int}/restore", async (int id, ProductService s, CancellationToken ct) =>
        {
            await s.SetActiveAsync(id, true, ct);
            return Results.NoContent();
        });
    }
}
