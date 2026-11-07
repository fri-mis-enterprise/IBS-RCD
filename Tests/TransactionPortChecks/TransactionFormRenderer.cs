using Accounting_System.Controllers;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class TransactionFormRenderer
{
    public static async Task RunAsync(string output)
    {
        var root = Directory.GetCurrentDirectory();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), ApplicationName = typeof(SalesInvoiceController).Assembly.GetName().Name });
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite("Data Source=:memory:"));
        builder.Services.AddDefaultIdentity<IdentityUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(SalesInvoiceController).Assembly);
        builder.Services.AddRazorPages();
        await using var app = builder.Build();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        Directory.CreateDirectory(output);
        var pages = new List<(string Module, string Action, object Model)>();
        foreach (var action in new[] { "Create", "Edit" })
        {
            pages.Add(("SalesInvoice", action, new SalesInvoice { Customers = [], Products = [] }));
            pages.Add(("ServiceInvoice", action, new ServiceInvoice { Customers = [], Services = [] }));
            pages.Add(("DebitMemo", action, new DebitMemo { SalesInvoices = [], ServiceInvoices = [], Source = "Sales Invoice" }));
            pages.Add(("CreditMemo", action, new CreditMemo { SalesInvoices = [], ServiceInvoices = [], Source = "Sales Invoice" }));
            pages.Add(("PurchaseOrder", action, new PurchaseOrder { Suppliers = [], Products = [], TriggerDate = new DateOnly(2026,11,6), TypeOfPurchase = "Spot" }));
            pages.Add(("ReceivingReport", action, new ReceivingReport { PurchaseOrders = [], SupplierDrNo = "DR123", WithdrawalCertificate = "WC123" }));
            pages.Add(("CheckVoucherTrade", action, new CheckVoucherTradeViewModel { Suppliers = [], BankAccounts = [], PONo = [], RR = [], RRs = [], COA = [], AccountNumber = [], AccountTitle = [], Debit = [], Credit = [], Documentation = new() { OldCVNo = "OLD123", IsDocumentedByOtherCompany = true, DocumentedByCompanyName = "Example" } }));
            pages.Add(("CheckVoucherNonTradeInvoice", action, new CheckVoucherNonTradeInvoicingViewModel { Suppliers = [], ChartOfAccounts = [], AccountingEntries = [new AccountingEntryViewModel()], Headers = new CheckVoucherHeader { Details = [] } }));
            pages.Add(("CheckVoucherNonTradePayment", action, new CheckVoucherNonTradePaymentViewModel { Suppliers = [], Banks = [], CheckVouchers = [], ChartOfAccounts = [], AccountNumber = [], AccountTitle = [], Debit = [], Credit = [], PaymentDetails = [] }));
            pages.Add(("JournalVoucher", action, action == "Create" ? new JournalVoucherVM { Header = new JournalVoucherHeader { COA = [], CheckVoucherHeaders = [], Details = [] }, Details = [] } : new JournalVoucherViewModel { COA = [], CheckVoucherHeaders = [], AccountNumber = [], AccountTitle = [], Debit = [], Credit = [] }));
        }
        foreach (var action in new[] { "CreatePayrollInvoice", "EditPayrollInvoice" })
            pages.Add(("CheckVoucherNonTradeInvoice", action, new CheckVoucherNonTradeInvoicingViewModel { Suppliers = [], ChartOfAccounts = [], DefaultExpenses = [], AccountingEntries = [new AccountingEntryViewModel()], Headers = new CheckVoucherHeader { Details = [] } }));
        foreach (var action in new[] { "SingleCollectionCreateForSales", "MultipleCollectionCreateForSales", "CollectionCreateForService", "CollectionEdit", "MultipleCollectionEdit" })
            pages.Add(("Receipt", action, new CollectionReceipt { Customers = [], SalesInvoices = [], ServiceInvoices = [], ChartOfAccounts = [], MultipleSIId = [], MultipleSI = [], SIMultipleAmount = [], MultipleTransactionDate = [], BatchNumber = "BATCH123", EwtReference1 = "EWT123" }));
        foreach (var page in pages)
        {
            using var scope = app.Services.CreateScope();
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            http.Request.Scheme = "http"; http.Request.Host = new HostString("localhost");
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Render checks"));
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
            var route = new RouteData(); route.Values["controller"] = page.Module; route.Values["action"] = page.Action; route.Values["id"] = 1;
            var context = new ActionContext(http, route, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
            var view = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>().GetView(null, $"/Views/{page.Module}/{page.Action}.cshtml", false);
            if (!view.Success) throw new Exception(string.Join(",", view.SearchedLocations));
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = page.Model };
            data["Offsettings"] = new List<Accounting_System.Models.Offsetting>();
            using var writer = new StringWriter();
            await view.View!.RenderAsync(new ViewContext(context, view.View, data, new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions()));
            await File.WriteAllTextAsync(Path.Combine(output, page.Module + "-" + page.Action + ".html"), writer.ToString());
            Console.WriteLine($"Rendered {page.Module}/{page.Action}");
        }
    }
}
