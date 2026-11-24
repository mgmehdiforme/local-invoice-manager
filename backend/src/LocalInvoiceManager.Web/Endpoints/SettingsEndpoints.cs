using LocalInvoiceManager.Application.Settings;

namespace LocalInvoiceManager.Web.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettings(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/settings").WithTags("Settings");

        g.MapGet("/", (SettingsService s, CancellationToken ct) => s.GetAsync(ct));
        g.MapPut("/", (SettingsDto dto, SettingsService s, CancellationToken ct) => s.UpdateAsync(dto, ct));
        g.MapPut("/preferences", async (PreferencesRequest req, SettingsService s, CancellationToken ct) =>
        {
            await s.SetPreferencesAsync(req, ct);
            return Results.NoContent();
        });

        g.MapGet("/logo", async (SettingsService s, CancellationToken ct) =>
        {
            var logo = await s.GetLogoAsync(ct);
            return logo is null ? Results.NotFound() : Results.File(logo.Value.Bytes, logo.Value.ContentType);
        });

        g.MapPost("/logo", async (IFormFile file, SettingsService s, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            await s.SetLogoAsync(file.FileName, stream, file.Length, ct);
            return Results.NoContent();
        }).DisableAntiforgery(); // validated by our own XSRF middleware

        g.MapDelete("/logo", async (SettingsService s, CancellationToken ct) =>
        {
            await s.RemoveLogoAsync(ct);
            return Results.NoContent();
        });
    }
}
