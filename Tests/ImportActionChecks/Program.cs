using System.Security.Claims;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.MasterFile;
using Accounting_System.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;

// Uses only an isolated in-memory database; never reads application connection strings.
ExcelPackage.License.SetNonCommercialPersonal("Local import regression checks");
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var aasConnection = new SqliteConnection("Data Source=:memory:");
await aasConnection.OpenAsync();
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
var services = new ServiceCollection().AddLogging();
services.AddScoped<ApplicationDbContext>(_ => new CheckDb(options));
services.AddScoped<AasDbContext>(_ => new CheckAasDb(new DbContextOptionsBuilder<AasDbContext>().UseSqlite(aasConnection).Options));
services.AddScoped<BankAccountRepo>();
services.AddScoped<ChartOfAccountRepo>();
services.AddScoped<ProductRepository>();
services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment, TestEnvironment>();
services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<ApplicationDbContext>();
foreach (var type in new[] { typeof(GeneralRepo), typeof(SalesInvoiceRepo), typeof(ServiceInvoiceRepo),
    typeof(PurchaseOrderRepo), typeof(ReceivingReportRepo), typeof(ReceiptRepo), typeof(CreditMemoRepo),
    typeof(DebitMemoRepo), typeof(CheckVoucherRepo), typeof(JournalVoucherRepo), typeof(CustomerRepo),
    typeof(SupplierRepo), typeof(ServiceRepo), typeof(InventoryRepo) }) services.AddScoped(type);
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.EnsureCreatedAsync();
var date = new DateTime(2026, 1, 15);
var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "import-check")], "test"));
var accounts = new[] { "101010100", "101020100", "101020200", "101020300", "101020500", "101060400", "101060600",
    "101060200", "201030210", "201010100", "202010100", "201030100", "401010100", "401020100", "501010100", "101040100" };
foreach (var account in accounts) db.ChartOfAccounts.Add(new ChartOfAccount { AccountNumber = account, AccountName = account });
db.Customers.Add(new Customer { OriginalCustomerId = 101, CustomerName = "Customer", CustomerAddress = "Address", CustomerTin = "TIN",
    BusinessStyle = "Trade", CustomerTerms = "COD", CustomerType = "Vatable" });
db.Products.Add(new Product { OriginalProductId = 201, ProductCode = "PET001", ProductName = "Fuel", ProductUnit = "L" });
db.Services.Add(new Services { OriginalServiceId = 301, Name = "Service", CurrentAndPreviousNo = "401020100", CurrentAndPreviousTitle = "Service revenue" });
db.Suppliers.Add(new Supplier { OriginalSupplierId = 401, SupplierName = "Supplier", SupplierAddress = "Address", SupplierTin = "TIN",
    SupplierTerms = "COD", VatType = "Vatable", TaxType = "Without Tax", Category = "Trade", Branch = "Main" });
db.BankAccounts.Add(new BankAccount { OriginalBankId = 501, Bank = "Bank", AccountName = "Account" });
await db.SaveChangesAsync();
var productId = (await db.Products.SingleAsync()).ProductId;
db.Inventories.Add(new Inventory { Date = new DateOnly(2026, 1, 1), ProductId = productId, Particular = "Beginning Balance",
    Quantity = 100, Cost = 5, Total = 500, InventoryBalance = 100, TotalBalance = 500, AverageCost = 5 });
await db.SaveChangesAsync();

void Assert(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
T Add<T>(T entity) where T : class
{
    foreach (var property in db.Entry(entity).Metadata.GetProperties())
        if (property.ClrType == typeof(string) && !property.IsNullable && db.Entry(entity).Property(property.Name).CurrentValue == null)
            db.Entry(entity).Property(property.Name).CurrentValue = string.Empty;
    db.Add(entity);
    return entity;
}
var customer = await db.Customers.SingleAsync();
var product = await db.Products.SingleAsync();
var supplier = await db.Suppliers.SingleAsync();
var service = await db.Services.SingleAsync();
var customer2 = Add(new Customer { OriginalCustomerId = 102, CustomerName = "Second customer" });
var product2 = Add(new Product { OriginalProductId = 202, ProductName = "Second product" });
var supplier2 = Add(new Supplier { OriginalSupplierId = 402, SupplierName = "Second supplier" });
var service2 = Add(new Services { OriginalServiceId = 302, Name = "Second service" });
await db.SaveChangesAsync();
var po = Add(new PurchaseOrder { OriginalDocumentId = 1201, OriginalSeriesNumber = "PO0001", PurchaseOrderNo = "LOCALPO1", ProductId = product.ProductId, SupplierId = supplier.SupplierId });
var po2 = Add(new PurchaseOrder { OriginalDocumentId = 1202, OriginalSeriesNumber = "PO0002", PurchaseOrderNo = "LOCALPO2", ProductId = product.ProductId, SupplierId = supplier.SupplierId });
var si = Add(new SalesInvoice { OriginalDocumentId = 601, OriginalSeriesNumber = "SI0001", SalesInvoiceNo = "LOCALSI1", CustomerId = customer.CustomerId, ProductId = product.ProductId, Status = "Pending" });
var si2 = Add(new SalesInvoice { OriginalDocumentId = 602, OriginalSeriesNumber = "SI0002", SalesInvoiceNo = "LOCALSI2", CustomerId = customer.CustomerId, ProductId = product.ProductId });
var sv = Add(new ServiceInvoice { OriginalDocumentId = 701, OriginalSeriesNumber = "SV0001", ServiceInvoiceNo = "LOCALSV1", CustomerId = customer.CustomerId, ServicesId = service.ServiceId });
var cv = Add(new CheckVoucherHeader { OriginalDocumentId = 901, OriginalSeriesNumber = "CV0001", CheckVoucherHeaderNo = "LOCALCV1", CvType = "Payment", IsPosted = true, PostedDate = date });
var cv2 = Add(new CheckVoucherHeader { OriginalDocumentId = 902, OriginalSeriesNumber = "CV0002", CheckVoucherHeaderNo = "LOCALCV2", Total = 100m });
var jv = Add(new JournalVoucherHeader { OriginalDocumentId = 1701, OriginalSeriesNumber = "JV0001", JournalVoucherHeaderNo = "LOCALJV1" });
var jv2 = Add(new JournalVoucherHeader { OriginalDocumentId = 1702, OriginalSeriesNumber = "JV0002", JournalVoucherHeaderNo = "LOCALJV2" });
var cm = Add(new CreditMemo { OriginalDocumentId = 1801, OriginalSeriesNumber = "CM0001", CreditMemoNo = "LOCALCM1" });
var dm = Add(new DebitMemo { OriginalDocumentId = 1901, OriginalSeriesNumber = "DM0001", DebitMemoNo = "LOCALDM1" });
await db.SaveChangesAsync();
var rr = Add(new ReceivingReport { OriginalDocumentId = 1101, OriginalSeriesNumber = "RR0001", ReceivingReportNo = "LOCALRR1", POId = po.PurchaseOrderId });
var rr2 = Add(new ReceivingReport { OriginalDocumentId = 1102, OriginalSeriesNumber = "RR0002", ReceivingReportNo = "LOCALRR2", POId = po.PurchaseOrderId });
var cr = Add(new CollectionReceipt { OriginalDocumentId = 801, OriginalSeriesNumber = "CR0001", CollectionReceiptNo = "LOCALCR1", CustomerId = customer.CustomerId, SalesInvoiceId = si.SalesInvoiceId });
var cvDetail = Add(new CheckVoucherDetail { OriginalDocumentId = 2001, CheckVoucherHeaderId = cv.CheckVoucherHeaderId, SupplierId = supplier.SupplierId, TransactionNo = cv.CheckVoucherHeaderNo! });
var jvDetail = Add(new JournalVoucherDetail { OriginalDocumentId = 2002, JournalVoucherHeaderId = jv.JournalVoucherHeaderId, TransactionNo = jv.JournalVoucherHeaderNo! });
await db.SaveChangesAsync();

var controller = new Accounting_System.Controllers.ImportExportLogsController(db, scope.ServiceProvider.GetRequiredService<AasDbContext>(), scope.ServiceProvider.GetRequiredService<GeneralRepo>());
var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user, RequestServices = scope.ServiceProvider };
controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http, RouteData = new Microsoft.AspNetCore.Routing.RouteData(), ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor() };
controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
var clock = new DateTime(2026, 10, 27);
var aas = scope.ServiceProvider.GetRequiredService<AasDbContext>();
await aas.Database.EnsureCreatedAsync();
foreach (var source in db.ChangeTracker.Entries().ToList())
{
    if (source.Entity is ImportExportLog) continue;
    var copy = source.CurrentValues.ToObject();
    aas.Add(copy);
}
await aas.SaveChangesAsync();
async Task<ImportExportLog> Proposal(object entity, string table, string column, string? value, DateTime? batch = null)
{
    var entry = db.Entry(entity);
    var id = entity is BaseEntity document ? document.OriginalDocumentId : entity is CheckVoucherDetail detail ? detail.OriginalDocumentId!.Value
        : entity is JournalVoucherDetail journalDetail ? journalDetail.OriginalDocumentId!.Value : Convert.ToInt32(entry.Property(entry.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue);
    var log = new ImportExportLog { Id = Guid.NewGuid(), DatabaseName = "IBS-RCD", TableName = table, ColumnName = column,
        DocumentRecordId = id, DocumentNo = entity is BaseEntity doc ? doc.OriginalSeriesNumber! : "DETAIL", Module = table,
        AdjustedValue = value, Action = "", TimeStamp = batch ?? (clock = clock.AddTicks(10)) };
    db.ImportExportLogs.Add(log);
    await db.SaveChangesAsync();
    return log;
}
async Task Modify(ImportExportLog log)
{
    var result = await controller.ImportAction(log.Id, "Modify", "UntrustedTable", "UntrustedColumn", CancellationToken.None);
    Assert(result is Microsoft.AspNetCore.Mvc.RedirectToActionResult,
        log.TableName + "/" + log.ColumnName + " applies: " + (result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult)?.Value);
    Assert(log.Action == "Modify" && log.Executed, "successful modification resolves its log");
}

foreach (var (entity, table) in new (BaseEntity, string)[] { (si, "SalesInvoice"), (sv, "ServiceInvoice"), (po, "PurchaseOrder"),
    (rr, "ReceivingReport"), (cr, "CollectionReceipt"), (cv, "CheckVoucherHeader"), (jv, "JournalVoucherHeader"), (cm, "CreditMemo"), (dm, "DebitMemo") })
{
    var wasPosted = entity.IsPosted;
    foreach (var column in new[] { "PostedBy", "PostedDate", "EditedBy", "EditedDate", "CanceledBy", "CanceledDate", "VoidedBy", "VoidedDate" })
    {
        var isDate = column.EndsWith("Date");
        var value = isDate ? "2026-10-27 14:15:16.1234567" : "Approved user";
        var log = await Proposal(entity, table, column, value);
        await Modify(log);
        await db.Entry(entity).ReloadAsync();
        Assert(Equals(db.Entry(entity).Property(column).CurrentValue, isDate ? new DateTime(2026, 10, 27, 14, 15, 16).AddTicks(1234567) : value), column + " persists in the database");
    }
    Assert(entity.IsPosted == wasPosted, "audit approvals do not silently post or unpost " + table);
}
await Modify(await Proposal(rr, "ReceivingReport", "CanceledQuantity", "12.3400"));
Assert(rr.CanceledQuantity == 12.34m, "RR canceled quantity is handled");
await Modify(await Proposal(rr, "ReceivingReport", "EditedDate", ""));
Assert(rr.EditedDate == null, "blank optional date clears its value");
await Modify(await Proposal(si, "SalesInvoice", "Status", "Posted"));
Assert(si.Status == "Posted" && si.IsPosted && !si.IsVoided && !si.IsCanceled, "Posted approval synchronizes SI lifecycle flags");
foreach (var status in new[] { "Voided", "Posted", "Canceled", "Cancelled", "Pending" })
{
    await Modify(await Proposal(si, "SalesInvoice", "Status", status));
    Assert(si.Status == status && si.IsPosted == (status == "Posted") &&
           si.IsVoided == (status == "Voided") && si.IsCanceled == (status is "Canceled" or "Cancelled"),
        status + " approval synchronizes SI lifecycle flags and clears previous flags");
}
var keepStatus = await Proposal(si, "SalesInvoice", "Status", "Voided");
Assert(await controller.ImportAction(keepStatus.Id, "Keep", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.RedirectToActionResult,
    "Keep SI status succeeds");
Assert(si.Status == "Pending" && !si.IsPosted && !si.IsVoided && !si.IsCanceled, "Keep SI status preserves lifecycle flags");
foreach (var status in new[] { "Posted", "Voided", "Posted", "Canceled", "Cancelled", "Pending", " posted ", "VOIDED" })
{
    await Modify(await Proposal(sv, "ServiceInvoice", "Status", status));
    await db.Entry(sv).ReloadAsync();
    var normalized = status.Trim().ToUpperInvariant();
    Assert(sv.Status == status && sv.IsPosted == (normalized == "POSTED") &&
           sv.IsVoided == (normalized == "VOIDED") && sv.IsCanceled == (normalized is "CANCELED" or "CANCELLED"),
        status + " approval persists synchronized Service Invoice flags");
}
var keepServiceStatus = await Proposal(sv, "ServiceInvoice", "Status", "Posted");
Assert(await controller.ImportAction(keepServiceStatus.Id, "Keep", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.RedirectToActionResult,
    "Keep Service Invoice status succeeds");
await db.Entry(sv).ReloadAsync();
Assert(sv.Status == "VOIDED" && sv.IsVoided && !sv.IsPosted && !sv.IsCanceled, "Keep Service Invoice status preserves stored lifecycle flags");
var keep = await Proposal(rr, "ReceivingReport", "Remarks", "Not approved");
var beforeKeep = rr.Remarks;
Assert(await controller.ImportAction(keep.Id, "Keep", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.RedirectToActionResult, "Keep succeeds");
Assert(rr.Remarks == beforeKeep && keep.Action == "Keep" && !keep.Executed, "Keep leaves database values unchanged");
Assert(await controller.ImportAction(keep.Id, "Modify", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.ConflictObjectResult, "resolved logs cannot be applied twice");

await Modify(await Proposal(si, "SalesInvoice", "OriginalCustomerId", "102"));
await Modify(await Proposal(si, "SalesInvoice", "OriginalProductId", "202"));
Assert(si.CustomerId == customer2.CustomerId && si.ProductId == product2.ProductId, "source customer/product IDs resolve to local foreign keys");
await Modify(await Proposal(sv, "ServiceInvoice", "OriginalServicesId", "302"));
Assert(sv.ServicesId == service2.ServiceId, "source service ID resolves locally");
await Modify(await Proposal(po, "PurchaseOrder", "OriginalSupplierId", "402"));
Assert(po.SupplierId == supplier2.SupplierId, "source supplier ID resolves locally");
await Modify(await Proposal(rr, "ReceivingReport", "OriginalPOId", "1202"));
Assert(rr.POId == po2.PurchaseOrderId && rr.PONo == po2.PurchaseOrderNo, "RR PO relationship and reference update together");
await Modify(await Proposal(cr, "CollectionReceipt", "OriginalSalesInvoiceId", "602"));
Assert(cr.SalesInvoiceId == si2.SalesInvoiceId && cr.SINo == si2.SalesInvoiceNo, "receipt source invoice reference resolves locally");
await Modify(await Proposal(jv, "JournalVoucherHeader", "OriginalCVId", "902"));
Assert(jv.CVId == cv2.CheckVoucherHeaderId, "JV source CV reference resolves locally");
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "SupplierId", "402"));
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "CheckVoucherHeaderId", "902"));
Assert(cvDetail.SupplierId == supplier2.SupplierId && cvDetail.CheckVoucherHeaderId == cv2.CheckVoucherHeaderId && cvDetail.TransactionNo == cv2.CheckVoucherHeaderNo,
    "CV detail supplier/header IDs and local transaction number update correctly");
await Modify(await Proposal(jvDetail, "JournalVoucherDetails", "JournalVoucherHeaderId", "1702"));
Assert(jvDetail.JournalVoucherHeaderId == jv2.JournalVoucherHeaderId && jvDetail.TransactionNo == jv2.JournalVoucherHeaderNo, "JV detail reference updates correctly");
await Modify(await Proposal(cv, "CheckVoucherHeader", "Reference", "CV0002"));
Assert(cv.Reference == cv2.CheckVoucherHeaderNo, "CV reference stores the local number for the approved source number");
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "TransactionNo", "CV0001"));
await Modify(await Proposal(jvDetail, "JournalVoucherDetails", "TransactionNo", "JV0001"));
Assert(cvDetail.CheckVoucherHeaderId == cv.CheckVoucherHeaderId && jvDetail.JournalVoucherHeaderId == jv.JournalVoucherHeaderId, "detail transaction numbers also keep their parent keys consistent");

await Modify(await Proposal(cr, "CollectionReceipt", "MultipleSI", "SI0001,SI0002"));
await Modify(await Proposal(cr, "CollectionReceipt", "MultipleSIId", "601,602"));
await Modify(await Proposal(cr, "CollectionReceipt", "SIMultipleAmount", "1,234.0000,5,678.9000"));
await Modify(await Proposal(cr, "CollectionReceipt", "MultipleTransactionDate", "2026-01-15,2026-01-16"));
Assert(cr.MultipleSI!.SequenceEqual(["SI0001", "SI0002"]) && cr.MultipleSIId!.SequenceEqual([si.SalesInvoiceId, si2.SalesInvoiceId]), "receipt arrays retain every value and use local invoice IDs");
Assert(cr.SIMultipleAmount!.SequenceEqual([1234m, 5678.9m]) && cr.MultipleTransactionDate!.Length == 2, "formatted amount/date arrays are parsed without losing entries");
await Modify(await Proposal(cv, "CheckVoucherHeader", "Amount", "1,234.0000,5,678.9000"));
Assert(cv.Amount!.SequenceEqual([1234m, 5678.9m]), "CV amount arrays retain grouped decimal values");
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "Amount", "12.3400"));
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "EwtPercent", "1.0000"));
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "IsVatable", "True"));
await Modify(await Proposal(cvDetail, "CheckVoucherDetails", "IsUserSelected", "True"));
Assert(cvDetail.Amount == 12.34m && cvDetail.EwtPercent == 1m && cvDetail.IsVatable && cvDetail.IsUserSelected, "all newly detected CV detail scalars apply");
foreach (var (entity, table, column, value) in new (object, string, string, string)[] {
    (await db.BankAccounts.SingleAsync(), "BankAccount", "AccountName", "Approved bank"),
    (customer, "Customer", "CustomerName", "Approved customer"), (supplier, "Supplier", "SupplierName", "Approved supplier"),
    (product, "Product", "ProductName", "Approved product"), (service, "Service", "Name", "Approved service"),
    (await db.ChartOfAccounts.FirstAsync(), "ChartOfAccount", "AccountName", "Approved account") })
{
    await Modify(await Proposal(entity, table, column, value));
    Assert((string?)db.Entry(entity).Property(column).CurrentValue == value, table + " master values are applied");
}

db.CVTradePayments.Add(new CVTradePayment { CheckVoucherId = cv.CheckVoucherHeaderId, DocumentId = rr.ReceivingReportId, DocumentType = "RR", AmountPaid = 10m });
await db.SaveChangesAsync();
var batch = clock = clock.AddTicks(10);
var payment = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.DocumentId", "1102", batch);
var paymentAmount = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "25.0000", batch);
await Modify(payment);
Assert(paymentAmount.Action == "Modify" && paymentAmount.Executed, "related payment fields are resolved as one approved group");
var savedPayment = await db.CVTradePayments.SingleAsync();
Assert(savedPayment.DocumentId == rr2.ReceivingReportId && savedPayment.AmountPaid == 25m, "payment group applies source report IDs and allocation amounts together");
Assert(rr.AmountPaid == 0m && rr2.AmountPaid == 25m, "payment approval recalculates affected RR payment totals");
var keepPayment = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "99.0000");
await controller.ImportAction(keepPayment.Id, "Keep", "", "", CancellationToken.None);
Assert((await db.CVTradePayments.SingleAsync()).AmountPaid == 25m && !keepPayment.Executed, "Keep payment group leaves allocations unchanged");
var multi = await Proposal(cv, "CheckVoucherHeader", "MultipleCheckVoucherPayments.CheckVoucherHeaderPaymentId", "901");
var multiBatch = multi.TimeStamp;
await Proposal(cv, "CheckVoucherHeader", "MultipleCheckVoucherPayments.CheckVoucherHeaderInvoiceId", "902", multiBatch);
await Proposal(cv, "CheckVoucherHeader", "MultipleCheckVoucherPayments.AmountPaid", "100.0000", multiBatch);
await Modify(multi);
Assert((await db.MultipleCheckVoucherPayments.SingleAsync()).CheckVoucherHeaderInvoiceId == cv2.CheckVoucherHeaderId && cv2.AmountPaid == 100m && cv2.IsPaid,
    "multiple-voucher allocation approval resolves source IDs and recalculates invoice payment totals");

foreach (var (column, value) in new[] { ("UnsupportedColumn", "x"), ("EditedDate", "invalid date"), ("AmountPaid", "999.0000"), ("OriginalPOId", "999999") })
{
    var record = await Proposal(rr, "ReceivingReport", column, value);
    var result = await controller.ImportAction(record.Id, "Modify", "", "", CancellationToken.None);
    Assert(result is Microsoft.AspNetCore.Mvc.BadRequestObjectResult, "invalid/unsupported change is rejected: " + column);
    var pending = await db.ImportExportLogs.AsNoTracking().SingleAsync(x => x.Id == record.Id);
    Assert(pending.Action == "" && !pending.Executed, "failed change stays pending: " + column);
}

{
    var before = await db.ImportExportLogs.Select(x => x.Id).ToListAsync();
    var repo = scope.ServiceProvider.GetRequiredService<CheckVoucherRepo>();
    var changes = repo.DetectTradePayments(
        [new Accounting_System.Models.ViewModels.CvTradePaymentUploadExcelFileViewModel { DocumentId = 1102, DocumentType = "RR", CheckVoucherId = 901, AmountPaid = 25m }],
        [new Accounting_System.Models.ViewModels.CvTradePaymentUploadExcelFileViewModel { DocumentId = 1101, DocumentType = "RR", CheckVoucherId = 901, AmountPaid = 35m }]);
    await repo.LogChangesAsync(901, changes, "import-check", "CV0001", "IBS-RCD");
    await db.SaveChangesAsync();
    var pending = await db.ImportExportLogs.Where(x => !before.Contains(x.Id)).ToListAsync();
    Assert(pending.Count == 2 && pending.Select(x => x.TimeStamp).Distinct().Count() == 1, "Detect-generated payment fields have one review timestamp");
    await Modify(pending[0]);
    Assert(pending.All(x => x.Action == "Modify" && x.Executed) && (await db.CVTradePayments.SingleAsync()).AmountPaid == 35m,
        "Detect-generated payment proposal is fully applied through ImportAction");
}
{
    clock = DateTime.Now.AddMinutes(1);
    var first = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.DocumentId", "", clock);
    var members = new List<ImportExportLog> { first };
    foreach (var field in new[] { "DocumentType", "CheckVoucherId", "AmountPaid" })
        members.Add(await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments." + field, "", clock));
    await Modify(first);
    Assert(!await db.CVTradePayments.AnyAsync() && members.All(x => x.Executed), "approved payment removal clears the complete group");
    Assert((await db.ReceivingReports.SingleAsync(x => x.OriginalDocumentId == 1101)).AmountPaid == 0m, "payment removal recalculates the previous RR total");
}
{
    var master = await db.Customers.SingleAsync(x => x.CustomerId == customer.CustomerId);
    var localKey = master.CustomerId;
    await Modify(await Proposal(master, "Customer", "OriginalCustomerId", "909"));
    Assert(master.CustomerId == localKey && master.OriginalCustomerId == 909, "master source identity changes never rewrite the local primary key");
}


{
    var target = await db.SalesInvoices.SingleAsync(x => x.OriginalDocumentId == 601);
    await Modify(await Proposal(target, "SalesInvoice", "SalesInvoiceNo", "LOCALSI-NEW"));
    Assert(target.SalesInvoiceNo == "LOCALSI-NEW", "detected invoice-number column names apply correctly");
    await Modify(await Proposal(target, "SalesInvoice", "SINo", "LOCALSI1"));
    Assert(target.SalesInvoiceNo == "LOCALSI1", "legacy invoice-number aliases remain supported");
    var report = await db.ReceivingReports.SingleAsync(x => x.OriginalDocumentId == 1101);
    await Modify(await Proposal(report, "ReceivingReport", "ReceivedDate", "0001-01-01"));
    Assert(report.ReceivedDate == null, "blank/minimum optional received date clears to null");
}
{
    var stamp = clock = clock.AddTicks(10);
    var first = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.DocumentId", "1102", stamp);
    var other = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "99.0000", stamp);
    await controller.ImportAction(first.Id, "Keep", "", "", CancellationToken.None);
    Assert(first.Action == "Keep" && other.Action == "Keep" && !first.Executed && !other.Executed && !await db.CVTradePayments.AnyAsync(),
        "Keep resolves every related payment proposal without changing allocations");
    var older = await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "1.0000");
    await Proposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "2.0000");
    Assert(await controller.ImportAction(older.Id, "Modify", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,
        "older payment proposals cannot overwrite a newer proposal");
    Assert((await db.ImportExportLogs.AsNoTracking().SingleAsync(x => x.Id == older.Id)).Action == "", "rejected older proposal remains pending");
}


using (var workbook = new ExcelPackage())
{
    var sheet = workbook.Workbook.Worksheets.Add("ReceivingReport");
    var headers = new[] { "Date", "DueDate", "SupplierInvoiceNumber", "SupplierInvoiceDate", "TruckOrVessels", "QuantityDelivered", "QuantityReceived", "GainOrLoss", "Amount", "OtherRef", "Remarks", "CreatedBy", "CreatedDate", "PostedBy", "PostedDate", "CancellationRemarks", "ReceivedDate", "OriginalPOId", "OriginalDocumentId", "OriginalRRNo", "CanceledBy", "CanceledDate", "VoidedBy", "VoidedDate", "EditedBy", "EditedDate" };
    for (var i = 0; i < headers.Length; i++) sheet.Cells[1, i + 1].Value = headers[i];
    sheet.Cells[2, 1].Value = date; sheet.Cells[2, 2].Value = date; sheet.Cells[2, 5].Value = "Truck";
    sheet.Cells[2, 6].Value = 0m; sheet.Cells[2, 7].Value = 0m; sheet.Cells[2, 8].Value = 0m; sheet.Cells[2, 9].Value = 0m;
    sheet.Cells[2, 11].Value = "Draft"; sheet.Cells[2, 13].Value = date; sheet.Cells[2, 18].Value = 1202;
    sheet.Cells[2, 19].Value = 1101; sheet.Cells[2, 20].Value = "RR0001";
    var before = (await db.ReceivingReports.SingleAsync(x => x.OriginalDocumentId == 1101)).CanceledQuantity;
    await ActivatorUtilities.CreateInstance<WorkbookImport>(scope.ServiceProvider).ImportAsync(workbook, "ReceivingReport", CancellationToken.None, user);
    Assert((await db.ReceivingReports.SingleAsync(x => x.OriginalDocumentId == 1101)).CanceledQuantity == before, "RR upload retains canceled quantity when its optional heading is absent");
    sheet.Cells[1, 27].Value = "CanceledQuantity"; sheet.Cells[2, 27].Value = 23.45m;
    await ActivatorUtilities.CreateInstance<WorkbookImport>(scope.ServiceProvider).ImportAsync(workbook, "ReceivingReport", CancellationToken.None, user);
    Assert((await db.ReceivingReports.SingleAsync(x => x.OriginalDocumentId == 1101)).CanceledQuantity == 23.45m, "RR upload round-trips the supplied canceled quantity");
}

async Task<ImportExportLog> AasProposal(object entity, string table, string column, string? value, DateTime? batch = null)
{
    var log = await Proposal(entity, table, column, value, batch);
    log.DatabaseName = "AAS";
    await db.SaveChangesAsync();
    return log;
}
foreach (var (entity, table) in new (BaseEntity, string)[] { (si, "SalesInvoice"), (sv, "ServiceInvoice"), (po, "PurchaseOrder"),
    (rr, "ReceivingReport"), (cr, "CollectionReceipt"), (cv, "CheckVoucherHeader"), (jv, "JournalVoucherHeader"), (cm, "CreditMemo"), (dm, "DebitMemo") })
{
    var target = await aas.FindAsync(entity.GetType(), db.Entry(entity).Properties.Where(x => x.Metadata.IsPrimaryKey()).Select(x => x.CurrentValue).ToArray());
    foreach (var prefix in new[] { "Posted", "Canceled", "Voided", "Edited" })
    {
        foreach (var (column, value) in new[] { (prefix + "By", "Approved AAS user"), (prefix + "Date", "2026-10-27 14:15:16.1234567"),
            (prefix + "By", ""), (prefix + "Date", "") })
        {
            await Modify(await AasProposal(entity, table, column, value));
            await aas.Entry(target!).ReloadAsync();
            Assert(Equals(aas.Entry(target!).Property(column).CurrentValue,
                column.EndsWith("By") ? value : value == "" ? null : new DateTime(2026, 10, 27, 14, 15, 16).AddTicks(1234567)),
                "AAS " + table + "/" + column + " persists independent optional audit values");
        }
    }
    if (aas.Entry(target!).Metadata.FindProperty("Status") != null)
    {
        foreach (var status in new[] { "Posted", "Voided", "Canceled", "Cancelled", "Pending" })
        {
            await Modify(await AasProposal(entity, table, "Status", status));
            await aas.Entry(target!).ReloadAsync();
            var doc = (BaseEntity)target!;
            Assert(doc.IsPosted == (status == "Posted") && doc.IsVoided == (status == "Voided") && doc.IsCanceled == (status is "Canceled" or "Cancelled"),
                "AAS " + table + " synchronizes " + status);
        }
    }
}
await Modify(await AasProposal(si, "SalesInvoice", "OriginalCustomerId", "102"));
Assert((await aas.SalesInvoices.FindAsync(si.SalesInvoiceId))!.CustomerId == customer2.CustomerId, "AAS source reference resolves in AAS");
await Modify(await AasProposal(rr, "ReceivingReport", "CanceledQuantity", "9.2500"));
Assert((await aas.ReceivingReports.FindAsync(rr.ReceivingReportId))!.CanceledQuantity == 9.25m, "AAS RR canceled quantity supported");
var aasKeep = await AasProposal(si, "SalesInvoice", "Status", "Voided");
Assert(await controller.ImportAction(aasKeep.Id, "Keep", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.RedirectToActionResult,
    "AAS Keep succeeds");
Assert((await aas.SalesInvoices.FindAsync(si.SalesInvoiceId))!.Status == "Pending" && !aasKeep.Executed, "AAS Keep preserves status");
await Modify(await AasProposal(cr, "CollectionReceipt", "MultipleSIId", "601,602"));
Assert((await aas.CollectionReceipts.FindAsync(cr.CollectionReceiptId))!.MultipleSIId!.SequenceEqual(new[] { si.SalesInvoiceId, si2.SalesInvoiceId }),
    "AAS receipt arrays resolve source IDs");
await Modify(await AasProposal(cvDetail, "CheckVoucherDetails", "SupplierId", "402"));
Assert((await aas.CheckVoucherDetails.FindAsync(cvDetail.CheckVoucherDetailId))!.SupplierId == supplier2.SupplierId, "AAS detail supplier resolves locally");
await Modify(await AasProposal(customer, "Customers", "CustomerName", "AAS customer"));
Assert((await aas.Customers.FindAsync(customer.CustomerId))!.CustomerName == "AAS customer", "AAS master fields supported");
var aasParent = await aas.ChartOfAccounts.FirstAsync();
var aasChild = await aas.ChartOfAccounts.FirstAsync(x => x.AccountId != aasParent.AccountId);
aasParent.OriginalChartOfAccountId = 91001;
await aas.SaveChangesAsync();
await Modify(await AasProposal(aasChild, "ChartOfAccount", "ParentAccountId", "91001"));
Assert(aasChild.ParentAccountId == aasParent.AccountId, "AAS COA parent resolves original source ID");
{
    var before = await db.ImportExportLogs.Select(x => x.Id).ToListAsync();
    var repo = scope.ServiceProvider.GetRequiredService<CheckVoucherRepo>();
    var changes = repo.DetectTradePayments([], [new Accounting_System.Models.ViewModels.CvTradePaymentUploadExcelFileViewModel
        { DocumentId = 1101, DocumentType = "RR", CheckVoucherId = 901, AmountPaid = 10.5m }]);
    await repo.LogChangesAsync(901, changes, "import-check", "CV0001", "AAS");
    await db.SaveChangesAsync();
    var generated = await db.ImportExportLogs.Where(x => !before.Contains(x.Id)).ToListAsync();
    Assert(generated.Count == 4 && generated.Select(x => x.TimeStamp).Distinct().Count() == 1, "AAS detected payment fields share one timestamp");
    await Modify(generated[0]);
    Assert(generated.All(x => x.Executed) && (await aas.CVTradePayments.SingleAsync()).AmountPaid == 10.5m,
        "AAS Detect to log to approved payment group works end to end");
}
var paymentBatch = clock.AddDays(3);
var paymentLogs = new List<ImportExportLog>();
foreach (var (column, value) in new[] { ("DocumentId", "1101"), ("DocumentType", "RR"), ("CheckVoucherId", "901"), ("AmountPaid", "15.2500") })
    paymentLogs.Add(await AasProposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments." + column, value, paymentBatch));
await Modify(paymentLogs[0]);
Assert(paymentLogs.All(x => x.Action == "Modify" && x.Executed), "AAS payment group resolves together");
Assert(await aas.CVTradePayments.AnyAsync(x => x.CheckVoucherId == cv.CheckVoucherHeaderId && x.DocumentId == rr.ReceivingReportId && x.AmountPaid == 15.25m),
    "AAS payment group saves local references and fractional amounts");
Assert((await aas.ReceivingReports.FindAsync(rr.ReceivingReportId))!.AmountPaid == 15.25m, "AAS payment totals reconciled");
// The status round-trip above now includes vouchers. Restore the posted payer for this payment fixture.
var postedPayer = (await aas.CheckVoucherHeaders.FindAsync(cv.CheckVoucherHeaderId))!;
postedPayer.IsPosted = true;
await aas.SaveChangesAsync();
var aasMultiBatch = paymentBatch.AddTicks(10);
var multiLogs = new List<ImportExportLog>();
foreach (var (column, value) in new[] { ("CheckVoucherHeaderPaymentId", "901"), ("CheckVoucherHeaderInvoiceId", "902"), ("AmountPaid", "100.0000") })
    multiLogs.Add(await AasProposal(cv, "CheckVoucherHeader", "MultipleCheckVoucherPayments." + column, value, aasMultiBatch));
await Modify(multiLogs[0]);
Assert((await aas.CheckVoucherHeaders.FindAsync(cv2.CheckVoucherHeaderId))!.AmountPaid == 100m, "AAS multiple voucher payments reconcile totals");
var retry = await AasProposal(si, "SalesInvoice", "Remarks", "AAS retry value");
await Modify(retry);
retry.Action = ""; retry.Executed = false;
await db.SaveChangesAsync();
var targetSi = (await aas.SalesInvoices.FindAsync(si.SalesInvoiceId))!;
targetSi.Remarks = "Subsequent AAS edit";
await aas.SaveChangesAsync();
await Modify(retry);
Assert(targetSi.Remarks == "Subsequent AAS edit", "AAS retry completes local log without applying the value twice");
var invalidAas = await AasProposal(si, "SalesInvoice", "PostedDate", "invalid-date");
Assert(await controller.ImportAction(invalidAas.Id, "Modify", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,
    "AAS invalid proposal rejected");
Assert(!await aas.ImportExportLogs.AnyAsync(x => x.Id == invalidAas.Id), "AAS invalid proposal has no completion receipt");
Assert((await db.ImportExportLogs.FindAsync(invalidAas.Id))!.Action == "", "AAS invalid proposal remains pending");
var staleAas = await AasProposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "17.2500", clock.AddDays(5));
var mirror = new ImportExportLog();
aas.Entry(mirror).CurrentValues.SetValues(staleAas);
aas.ImportExportLogs.Add(mirror);
await aas.SaveChangesAsync();
await AasProposal(cv, "CheckVoucherHeader", "CheckVoucherTradePayments.AmountPaid", "18.2500", clock.AddDays(6));
Assert(await controller.ImportAction(staleAas.Id, "Modify", "", "", CancellationToken.None) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,
    "pending AAS mirror does not bypass newer payment proposal validation");
Assert((await db.ImportExportLogs.FindAsync(staleAas.Id))!.Action == "", "stale mirrored AAS proposal remains pending");
Console.WriteLine("All IBS-RCD and AAS ImportAction checks passed using isolated databases.");
sealed class CheckDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    public List<List<(string? Series, decimal Quantity)>> SavedReportBatches { get; } = [];

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var batch = ChangeTracker.Entries<ReceivingReport>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => (x.Entity.ReceivingReportNo, x.Entity.QuantityReceived)).ToList();
        var result = await base.SaveChangesAsync(cancellationToken);
        if (batch.Count > 0) SavedReportBatches.Add(batch);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Production uses PostgreSQL column types; this isolated check uses SQLite's native mappings.
        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnType(null);
    }
}

sealed class CheckAasDb(DbContextOptions<AasDbContext> options) : AasDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnType(null);
    }
}

sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public string ApplicationName { get; set; } = "Checks";
    public string EnvironmentName { get; set; } = "Development";
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
sealed class TestTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
}
