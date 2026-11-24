using LocalInvoiceManager.Domain.Localization;
using FluentValidation;
using LocalInvoiceManager.Application.Auth;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Customers;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Application.Notifications;
using LocalInvoiceManager.Application.Payments;
using LocalInvoiceManager.Application.Recurring;
using LocalInvoiceManager.Application.Quotations;
using LocalInvoiceManager.Application.Reports;
using LocalInvoiceManager.Application.Products;
using LocalInvoiceManager.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace LocalInvoiceManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Built-in validator messages follow the request culture (set from Accept-Language by the web layer).
        ValidatorOptions.Global.DisplayNameResolver = (_, _, _) => Loc.T("این فیلد");
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<AuditLogger>();
        services.AddScoped<AuthService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<NumberingService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ProductService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<DocumentPdfService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<ReportService>();
        services.AddScoped<QuotationService>();
        services.AddScoped<CustomerPriceService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<RecurringInvoiceService>();
        return services;
    }
}
