using Accounting_System.Controllers;
using Accounting_System.Models;
using Accounting_System.Models.MasterFile;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

var root = Directory.GetCurrentDirectory();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), ApplicationName = typeof(CustomerController).Assembly.GetName().Name });
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CustomerController).Assembly);
await using var app = builder.Build();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "ibs-rcd-rendered-masters");
Directory.CreateDirectory(output);
foreach (var module in new[] { "Customer", "Supplier", "Product", "BankAccount", "Service", "ChartOfAccount" })
foreach (var action in new[] { "Create", "Edit" })
{
    using var scope = app.Services.CreateScope();
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Render checks"));
    http.Request.Scheme = "http";
    http.Request.Host = new HostString("localhost");
    var route = new RouteData(); route.Values["controller"] = module; route.Values["action"] = action; route.Values["id"] = 1;
    var context = new ActionContext(http, route, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
    var view = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>().GetView(null, $"/Views/{module}/{action}.cshtml", false);
    if (!view.Success) throw new Exception(string.Join(",", view.SearchedLocations));
    object model = module switch {
        "Customer" => new Customer { CustomerId = 1, CustomerName = "Example", CustomerType = "Retail", CustomerTerms = "30D", VatType = "Vatable", WithHoldingTax = true, CwtPercent = .02m },
        "Supplier" => new Supplier { SupplierId = 1, Category = "Employee", TaxType = "Exempt", VatType = "Vatable", SupplierTerms = "30D", EmployeeNumber = "E001", Validity = "Temporary", ValidityDate = new DateTime(2027,1,1), DefaultExpenses = [], WithholdingTaxList = [], ProofOfRegistrationFilePath = "saved", ProofOfExemptionFilePath = "saved" },
        "Product" => new Product { ProductId = 1 },
        "BankAccount" => new BankAccount { BankAccountId = 1 },
        "Service" => new Services { ServiceId = 1, CurrentAndPreviousTitles = [], UnearnedTitles = [] },
        _ => new ChartOfAccount { Main = [] }
    };
    var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
    data["Commissionees"] = new List<SelectListItem>();
    var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    using var writer = new StringWriter();
    await view.View!.RenderAsync(new ViewContext(context, view.View, data, temp, writer, new HtmlHelperOptions()));
    await File.WriteAllTextAsync(Path.Combine(output, $"{module}-{action}.html"), writer.ToString());
    Console.WriteLine($"Rendered {module}/{action}");
}
