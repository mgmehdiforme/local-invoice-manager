using LocalInvoiceManager.Application.Auth;
using LocalInvoiceManager.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LocalInvoiceManager.Web.Endpoints;

public static class AuthEndpoints
{
    public sealed record PasswordRequest(string Password);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public static void MapAuth(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/auth").WithTags("Auth");

        g.MapGet("/status", async (HttpContext ctx, AuthService auth, CancellationToken ct) =>
        {
            var status = await auth.GetStatusAsync(ct);
            return Results.Ok(new
            {
                status.IsSetUp,
                IsAuthenticated = ctx.User.Identity?.IsAuthenticated == true,
                status.SessionTimeoutMinutes,
                status.Language,
                status.Calendar,
            });
        }).AllowAnonymous();

        g.MapGet("/csrf", (HttpContext ctx) =>
        {
            Security.IssueXsrfToken(ctx);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapPost("/setup", async (SetupRequest req, HttpContext ctx, AuthService auth, CancellationToken ct) =>
        {
            var result = await auth.SetupAsync(req, ct);
            await Security.SignInAsync(ctx, result.SecurityStamp);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/login", async (PasswordRequest req, HttpContext ctx, AuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(req.Password, ct);
            await Security.SignInAsync(ctx, result.SecurityStamp);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapPost("/change-password", async (ChangePasswordRequest req, HttpContext ctx, AuthService auth, CancellationToken ct) =>
        {
            var result = await auth.ChangePasswordAsync(req.CurrentPassword, req.NewPassword, ct);
            await Security.SignInAsync(ctx, result.SecurityStamp); // keep this session, end all others
            return Results.NoContent();
        }).RequireRateLimiting("login");
    }
}
