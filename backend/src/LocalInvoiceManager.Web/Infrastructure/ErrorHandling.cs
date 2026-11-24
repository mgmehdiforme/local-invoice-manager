using LocalInvoiceManager.Domain.Localization;
using FluentValidation;
using LocalInvoiceManager.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Web.Infrastructure;

/// <summary>Turns exceptions into Problem Details with a stable <c>code</c> and a <c>detail</c> in the request language.</summary>
public sealed class ErrorHandler(ILogger<ErrorHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, code, detail, errors) = ex switch
        {
            NotFoundException nf => (404, nf.Code, Loc.Translate(nf.Message), null),
            DomainException d when d.Code == "auth.locked_out" => (429, d.Code, Loc.Translate(d.Message), null),
            DomainException d => (400, d.Code, Loc.Translate(d.Message), null),
            ValidationException v => (400, "validation", Loc.T("اطلاعات وارد شده معتبر نیست."), ToErrors(v)),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } } =>
                (409, "conflict", Loc.T("این اطلاعات با رکورد دیگری تداخل دارد (مقدار تکراری)."), null),
            BadHttpRequestException b => (b.StatusCode, "bad_request", Loc.T("درخواست نامعتبر است."), null),
            OperationCanceledException when ctx.RequestAborted.IsCancellationRequested => (499, "cancelled", "", null),
            _ => (500, "server_error", Loc.T("خطای غیرمنتظره رخ داد. جزئیات در لاگ برنامه ثبت شده است."), (Dictionary<string, string[]>?)null),
        };

        if (status >= 500) logger.LogError(ex, "Unhandled error on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
        else logger.LogInformation("Request failed {Status} {Code} on {Method} {Path}", status, code, ctx.Request.Method, ctx.Request.Path);

        ctx.Response.StatusCode = status;
        var pd = new ProblemDetails { Status = status, Title = code, Detail = detail };
        pd.Extensions["code"] = code;
        if (errors is not null) pd.Extensions["errors"] = errors;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = pd, Exception = ex });
    }

    private static Dictionary<string, string[]> ToErrors(ValidationException v) =>
        v.Errors
            .GroupBy(e => CamelPath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => Loc.Translate(e.ErrorMessage)).Distinct().ToArray());

    private static string CamelPath(string path) =>
        string.Join('.', path.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
