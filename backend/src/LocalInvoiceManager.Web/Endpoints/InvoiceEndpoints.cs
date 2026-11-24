using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Invoices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace LocalInvoiceManager.Web.Endpoints;

public static class InvoiceEndpoints
{
    public sealed record ReasonRequest(string? Reason);

    public static void MapInvoices(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/invoices").WithTags("Invoices");
        g.MapGet("/", ([AsParameters] InvoiceQuery q, InvoiceService s, CancellationToken ct) => s.ListAsync(q, ct));
        g.MapGet("/defaults", (InvoiceService s, CancellationToken ct) => s.DefaultsAsync(ct));
        g.MapGet("/{id:int}", (int id, InvoiceService s, CancellationToken ct) => s.GetAsync(id, ct));
        g.MapPost("/calculate", (CalculateInput input, InvoiceService s, CancellationToken ct) => s.CalculateAsync(input, ct));
        g.MapPost("/", async (InvoiceInput input, InvoiceService s, CancellationToken ct) =>
        {
            var inv = await s.CreateDraftAsync(input, ct);
            return Results.Created($"/api/v1/invoices/{inv.Id}", inv);
        });
        g.MapPut("/{id:int}", (int id, InvoiceInput input, InvoiceService s, CancellationToken ct) => s.UpdateDraftAsync(id, input, ct));
        g.MapDelete("/{id:int}", async (int id, InvoiceService s, CancellationToken ct) =>
        {
            await s.DeleteDraftAsync(id, ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:int}/issue", (int id, InvoiceService s, CancellationToken ct) => s.IssueAsync(id, ct));
        g.MapPost("/{id:int}/cancel", (int id, ReasonRequest req, InvoiceService s, CancellationToken ct) => s.CancelAsync(id, req.Reason, ct));
        g.MapPost("/{id:int}/duplicate", (int id, InvoiceService s, CancellationToken ct) => s.DuplicateAsync(id, ct));
        g.MapGet("/{id:int}/pdf", async (int id, string? template, bool? download, DocumentPdfService pdf, CancellationToken ct) =>
        {
            var (bytes, name) = await pdf.InvoicePdfAsync(id, template, ct);
            return PdfResult(bytes, name, download == true);
        });

        api.MapGet("/customers/{id:int}/invoices", ([AsParameters] InvoiceQuery q, int id, InvoiceService s, CancellationToken ct) =>
            s.ListAsync(q with { CustomerId = id }, ct)).WithTags("Customers");
    }

    /// <summary>Inline by default so the browser can show and print it; attachment when downloading.</summary>
    public static IResult PdfResult(byte[] bytes, string fileName, bool download)
    {
        var cd = new ContentDispositionHeaderValue(download ? "attachment" : "inline");
        cd.SetHttpFileName(fileName);
        return new PdfFileResult(bytes, cd.ToString());
    }

    private sealed class PdfFileResult(byte[] bytes, string disposition) : IResult
    {
        public async Task ExecuteAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/pdf";
            ctx.Response.Headers.ContentDisposition = disposition;
            ctx.Response.ContentLength = bytes.Length;
            await ctx.Response.Body.WriteAsync(bytes);
        }
    }
}
