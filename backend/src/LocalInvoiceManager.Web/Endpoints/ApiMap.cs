namespace LocalInvoiceManager.Web.Endpoints;

public static class ApiMap
{
    public static void MapApi(this WebApplication app, RouteGroupBuilder api)
    {
        api.MapAuth();
        api.MapSettings();
        api.MapCustomers();
        api.MapProducts();
        api.MapInvoices();
        api.MapFinance();
        api.MapQuotations();
        api.MapAutomation();
    }
}
