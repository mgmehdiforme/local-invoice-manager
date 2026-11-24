using LocalInvoiceManager.Application.Customers;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Quotations;
using Microsoft.AspNetCore.Mvc;

namespace LocalInvoiceManager.Web.Endpoints;

public static class QuotationEndpoints
{
    public static void MapQuotations(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/quotations").WithTags("Quotations");
        g.MapGet("/", ([AsParameters] QuotationQuery q, QuotationService s, CancellationToken ct) => s.ListAsync(q, ct));
        g.MapGet("/{id:int}", (int id, QuotationService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/", async (QuotationInput input, QuotationService s, CancellationToken ct) =>
        {
            var q = await s.CreateAsync(input, ct);
            return Results.Created($"/api/v1/quotations/{q.Id}", q);
        });
        g.MapPut("/{id:int}", (int id, QuotationInput input, QuotationService s, CancellationToken ct) => s.UpdateAsync(id, input, ct));
        g.MapDelete("/{id:int}", async (int id, QuotationService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:int}/send", (int id, QuotationService s, CancellationToken ct) => s.SendAsync(id, ct));
        g.MapPost("/{id:int}/accept", (int id, QuotationService s, CancellationToken ct) => s.AcceptAsync(id, ct));
        g.MapPost("/{id:int}/reject", (int id, QuotationService s, CancellationToken ct) => s.RejectAsync(id, ct));
        g.MapPost("/{id:int}/convert", (int id, QuotationService s, CancellationToken ct) => s.ConvertAsync(id, ct));
        g.MapPost("/{id:int}/duplicate", (int id, QuotationService s, CancellationToken ct) => s.DuplicateAsync(id, ct));
        g.MapGet("/{id:int}/pdf", async (int id, string? template, bool? download, DocumentPdfService pdf, CancellationToken ct) =>
        {
            var (bytes, name) = await pdf.QuotationPdfAsync(id, template, ct);
            return InvoiceEndpoints.PdfResult(bytes, name, download == true);
        });

        api.MapGet("/customers/{id:int}/prices", (int id, CustomerPriceService s, CancellationToken ct) => s.GetAsync(id, ct)).WithTags("Customers");
        api.MapPut("/customers/{id:int}/prices", (int id, List<CustomerPriceInput> prices, CustomerPriceService s, CancellationToken ct) => s.ReplaceAsync(id, prices, ct)).WithTags("Customers");
    }
}
