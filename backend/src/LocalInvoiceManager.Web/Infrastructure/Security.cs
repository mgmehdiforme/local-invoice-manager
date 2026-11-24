using LocalInvoiceManager.Domain.Localization;
using System.Security.Claims;
using LocalInvoiceManager.Application.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LocalInvoiceManager.Web.Infrastructure;

public static class Security
{
    public const string SessionCookie = "lim.session";
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";
    public const string StampClaim = "stamp";
    private const string LastSeenKey = "last";

    public static IServiceCollection AddLocalSecurity(this IServiceCollection services, IConfiguration config)
    {
        // Secure flag only over HTTPS: browsers differ on accepting Secure cookies on http://localhost,
        // and traffic never leaves this computer (docs/SECURITY.md). Set Security:SecureCookies=true behind HTTPS.
        var secure = config.GetValue("Security:SecureCookies", false) ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = SessionCookie;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = secure;
                o.ExpireTimeSpan = TimeSpan.FromHours(12); // absolute cap; idle timeout enforced below
                o.SlidingExpiration = false;
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = ValidateSessionAsync;
            });
        services.AddAuthorization();

        services.AddAntiforgery(o =>
        {
            o.HeaderName = XsrfHeader;
            o.Cookie.Name = "lim.af";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = secure;
        });
        return services;
    }

    /// <summary>Rejects the session if the password changed (security stamp) or the user was idle longer than the timeout.</summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext ctx)
    {
        var auth = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
        var time = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var stamp = ctx.Principal?.FindFirstValue(StampClaim);
        var (valid, timeoutMinutes) = await auth.ValidateSessionAsync(stamp, ctx.HttpContext.RequestAborted);

        var now = time.GetUtcNow();
        var last = ctx.Properties.Items.TryGetValue(LastSeenKey, out var raw) && long.TryParse(raw, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero) : (DateTimeOffset?)null;

        if (!valid || last is null || now - last.Value > TimeSpan.FromMinutes(timeoutMinutes))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Status polling must not keep the session alive forever.
        var isPassive = ctx.HttpContext.Request.Path.StartsWithSegments("/api/v1/auth/status")
            || ctx.HttpContext.Request.Path.StartsWithSegments("/api/v1/notifications/count");
        if (!isPassive && now - last.Value > TimeSpan.FromSeconds(30))
        {
            ctx.Properties.Items[LastSeenKey] = now.UtcTicks.ToString();
            ctx.ShouldRenew = true;
        }
    }

    public static async Task SignInAsync(HttpContext ctx, string securityStamp)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "owner"), new Claim(StampClaim, securityStamp)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties { IsPersistent = false };
        props.Items[LastSeenKey] = ctx.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcTicks.ToString();
        var principal = new ClaimsPrincipal(identity);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
        ctx.User = principal; // so the new XSRF token is bound to the signed-in user
        IssueXsrfToken(ctx);
    }

    /// <summary>Writes a JS-readable XSRF-TOKEN cookie; the SPA echoes it in the X-XSRF-TOKEN header.</summary>
    public static void IssueXsrfToken(HttpContext ctx)
    {
        var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var secure = ctx.RequestServices.GetRequiredService<IConfiguration>().GetValue("Security:SecureCookies", false);
        ctx.Response.Cookies.Append(XsrfCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = secure || ctx.Request.IsHttps,
            Path = "/",
        });
    }

    /// <summary>Validates the XSRF header on every state-changing API request.</summary>
    public static IApplicationBuilder UseXsrfValidation(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var method = ctx.Request.Method;
        var unsafeMethod = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
        if (unsafeMethod && ctx.Request.Path.StartsWithSegments("/api"))
        {
            var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(ctx))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    status = 400, title = "xsrf", code = "xsrf",
                    detail = Loc.T("نشست امنیتی منقضی شده است. صفحه را دوباره بارگذاری کنید."),
                });
                return;
            }
        }
        await next();
    });

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "SAMEORIGIN";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
            "font-src 'self' data:; connect-src 'self'; frame-src 'self' blob:; object-src 'self' blob:; " +
            "base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
        if (ctx.Request.Path.StartsWithSegments("/api"))
            h.CacheControl = "no-store";
        await next();
    });
}
