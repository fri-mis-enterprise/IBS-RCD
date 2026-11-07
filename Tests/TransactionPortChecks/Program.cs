using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Repository;
using Accounting_System.Models.MasterFile;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Reflection;

if (args.Length > 0 && args[0] == "render") { await TransactionFormRenderer.RunAsync(args[1]); return; }
ExcelPackage.License.SetNonCommercialPersonal("Transaction port checks");
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var db = new CheckDb(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
await db.Database.EnsureCreatedAsync();
if (db.Model.GetEntityTypes().Any(e => e.FindProperty("Type") != null))
    throw new Exception("Document classification Type must not be mapped in any module.");
T Add<T>(T value) where T : class
{
    foreach (var property in db.Entry(value).Metadata.GetProperties())
        if (property.ClrType == typeof(string) && !property.IsNullable && db.Entry(value).Property(property.Name).CurrentValue == null)
            db.Entry(value).Property(property.Name).CurrentValue = "";
    db.Add(value); return value;
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
var customer = Add(new Customer { CustomerName = "Original name", CustomerAddress = "Original address", CustomerTin = "TIN", CustomerTerms = "30D", VatType = "Vatable" });
var supplier = Add(new Supplier { SupplierName = "Original supplier", SupplierAddress = "Address", SupplierTin = "TIN", WithholdingTaxPercent = 2m });
var product = Add(new Product { ProductName = "Fuel" });
var service = Add(new Services { Name = "Service", Percent = 5 });
var bank = Add(new BankAccount { AccountName = "Operating", AccountNo = "001234" });
await db.SaveChangesAsync();
var invoice = Add(new SalesInvoice { CustomerId = customer.CustomerId, ProductId = product.ProductId, Quantity = 10m, UnitPrice = 20m, Amount = 200m, Balance = 200m });
var sv = Add(new ServiceInvoice { CustomerId = customer.CustomerId, ServicesId = service.ServiceId, Amount = 123m, Total = 123m });
var po = Add(new PurchaseOrder { SupplierId = supplier.SupplierId, ProductId = product.ProductId, Amount = 200m, Date = new DateOnly(2026,11,6) });
var cv = Add(new CheckVoucherHeader { SupplierId = supplier.SupplierId, BankId = bank.BankAccountId, Total = 99m });
await TransactionDraftFields.PrepareAsync(db, default);
Check(invoice.CustomerAddress == "Original address" && sv.CustomerName == "Original name", "Invoice customer snapshots are captured");
Check(sv.ServiceName == "Service" && sv.ServicePercent == 5m, "Service snapshots are captured");
Check(po.SupplierName == "Original supplier" && po.ProductName == "Fuel", "PO supplier and product snapshots are captured");
Check(cv.BankAccountNumber == "001234" && cv.TaxPercent == 2m, "Voucher bank and supplier metadata is captured");
Check(invoice.Amount == 200m && invoice.Balance == 200m && sv.Amount == 123m && cv.Total == 99m, "Snapshot capture does not recalculate RCD financial amounts");
await db.SaveChangesAsync();
invoice.IsPosted = true;
await db.SaveChangesAsync();
customer.CustomerAddress = "Changed master";
invoice.Remarks = "Test a posted row";
await TransactionDraftFields.PrepareAsync(db, default);
Check(invoice.CustomerAddress == "Original address", "Posted invoice snapshots are not refreshed");
Check(await db.GeneralLedgerBooks.CountAsync() == 0 && await db.SalesBooks.CountAsync() == 0 && await db.DisbursementBooks.CountAsync() == 0, "Draft metadata produces no book or ledger entries");
var columns = typeof(TransactionDraftFields).Assembly.GetType("Accounting_System.Utility.TransactionColumns")!;
var import = columns.GetMethod("Import", BindingFlags.Public|BindingFlags.Static)!;
using var package = new ExcelPackage();
var sheet = package.Workbook.Worksheets.Add("PurchaseOrder");
sheet.Cells[1,1].Value = "SupplierSalesOrderNo"; sheet.Cells[2,1].Value = "SO-123";
sheet.Cells[1,2].Value = "TriggerDate"; sheet.Cells[2,2].Value = "2026-11-06";
var previous = new PurchaseOrder { SupplierName = "Retain snapshot", OldPoNo = "Legacy" };
var imported = new PurchaseOrder();
import.Invoke(null, [imported, previous, sheet, 2]);
Check(imported.SupplierSalesOrderNo == "SO-123" && imported.TriggerDate == new DateOnly(2026,11,6), "New workbook fields are parsed");
Check(imported.SupplierName == "Retain snapshot" && imported.OldPoNo == "Legacy", "Missing optional headers preserve existing transaction metadata");
var export = columns.GetMethod("Export")!.MakeGenericMethod(typeof(PurchaseOrder));
var roundTrip = package.Workbook.Worksheets.Add("RoundTrip"); roundTrip.Cells[1,1].Value="ExistingColumn";
export.Invoke(null,[roundTrip,new[]{imported}]);
var restored = new PurchaseOrder(); import.Invoke(null,[restored,null,roundTrip,2]);
Check(restored.SupplierSalesOrderNo == "SO-123" && restored.SupplierName == "Retain snapshot", "Supplemental fields round-trip through Excel");
Check(!roundTrip.Cells[1, 1, 1, roundTrip.Dimension.End.Column].Any(c => c.Text == "Type"), "Removed Type column is not exported");
roundTrip.Cells[1, roundTrip.Dimension.End.Column + 1].Value = "Type";
roundTrip.Cells[2, roundTrip.Dimension.End.Column].Value = "Undocumented";
try { import.Invoke(null, [restored, null, roundTrip, 2]); throw new Exception("Undocumented legacy input was accepted"); }
catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { Console.WriteLine("PASS Undocumented legacy input remains rejected"); }
var arrays = package.Workbook.Worksheets.Add("CollectionReceipt");
arrays.Cells[1,1].Value="MultipleSVId";arrays.Cells[2,1].Value="12, 34";
arrays.Cells[1,2].Value="SVMultipleAmount";arrays.Cells[2,2].Value="1,000.0000, 2,000.0000";
var receipt = new CollectionReceipt(); import.Invoke(null,[receipt,null,arrays,2]);
Check(receipt.MultipleSVId!.SequenceEqual(new[]{12,34}) && receipt.SVMultipleAmount!.SequenceEqual(new[]{1000m,2000m}),"IBSWeb comma-separated service allocations parse without losing thousands separators");
var docs = new Accounting_System.Models.ViewModels.CheckVoucherDocumentationViewModel { OldCVNo="OLD", DcpDate=new DateOnly(2026,11,6) };
docs.ApplyTo(cv);Check(cv.OldCvNo=="OLD" && cv.DcpDate==new DateOnly(2026,11,6),"Voucher documentation fields are persisted independently of posting");
await db.SaveChangesAsync();
Console.WriteLine("Transaction field and posting-isolation checks passed.");
sealed class CheckDb(DbContextOptions<ApplicationDbContext> options):ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach(var entity in builder.Model.GetEntityTypes()) foreach(var property in entity.GetProperties()) property.SetColumnType(null);
    }
}
