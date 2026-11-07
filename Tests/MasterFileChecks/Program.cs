using Accounting_System.Controllers;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.MasterFile;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

ExcelPackage.License.SetNonCommercialPersonal("Local master file regression checks");
var supplierJson = System.Text.Json.JsonSerializer.Serialize(new Supplier(), System.Text.Json.JsonSerializerOptions.Web);
Assert(supplierJson.Contains("withholdingTaxtitle"), "Supplier JSON preserves the existing table-data field name");
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var db = new CheckDb(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
await db.Database.EnsureCreatedAsync();
foreach (var entityType in db.Model.GetEntityTypes().Select(e => e.ClrType).Distinct()
             .Where(t => t.Namespace?.StartsWith("Accounting_System.Models") == true && t.GetConstructor(Type.EmptyTypes) != null))
{
    System.Text.Json.JsonSerializer.Serialize(Activator.CreateInstance(entityType), entityType,
        System.Text.Json.JsonSerializerOptions.Web);
}
Console.WriteLine("All module entity JSON contracts serialize with ASP.NET web settings.");
var supplier = new Supplier
{
    SupplierName = "Commissionee", SupplierAddress = "Address", SupplierTin = "000-000-000-00000",
    SupplierTerms = "COD", VatType = "Vatable", TaxType = "Withholding Tax", Category = "Commissionee",
    Branch = "Main", SupplierCode = "S000009", WithholdingTaxPercent = 0.5m
};
db.Suppliers.Add(supplier);
var bank = new BankAccount { Bank = "TEST", AccountName = "Operating", Branch = "Main", AccountNo = "001234567890" };
db.BankAccounts.Add(bank);
await db.SaveChangesAsync();
var customer = new Customer
{
    CustomerName = "Customer", CustomerAddress = "Address", CustomerTin = "000-000-000-00000",
    CustomerTerms = "COD", CustomerType = "Industrial", BusinessStyle = "Trading", ZipCode = "1000",
    CustomerCode = "IND0012", VatType = "Exempt", CwtPercent = 0.005m, CwVatPercent = 0.02m,
    CommissioneeId = supplier.SupplierId, CreditLimit = 50000m, RetentionRate = 0.05m,
    Branches = [new CustomerBranch { BranchName = "Branch", BranchAddress = "Branch address", BranchTin = "Branch TIN" }]
};
db.Customers.Add(customer);
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
var loaded = await db.Customers.Include(c => c.Commissionee).Include(c => c.Branches).SingleAsync();
Assert(loaded.IsActive && loaded.VatType == "Exempt" && loaded.CustomerType == "Industrial", "Separate VAT classification persists");
Assert(loaded.Commissionee!.WithholdingTaxPercent == 0.5m && loaded.Branches.Single().BranchName == "Branch", "Fractional supplier rates and customer relationships persist");
Assert(await new CustomerRepo(db).GenerateCodeAsync("Industrial", default) == "IND0013", "Customer code increments within category");
Assert(await new CustomerRepo(db).GenerateCodeAsync("Retail", default) == "RET0001", "New category starts its own series");
Assert(await new SupplierRepo(db).GenerateCodeAsync(default) == "S000010", "Supplier code increments");
var bankController = new BankAccountController(db, null!, new BankAccountRepo(db), null!, null!);
var result = await bankController.Export(bank.BankAccountId.ToString(), default);
Assert(result is FileContentResult, "Bank export succeeds");
using (var package = new ExcelPackage(new MemoryStream(((FileContentResult)result).FileContents)))
{
    var sheet = package.Workbook.Worksheets[0];
    var headers = Enumerable.Range(1, sheet.Dimension.Columns).Select(c => sheet.Cells[1, c].Text).ToList();
    Assert(headers.Distinct().Count() == headers.Count, "Bank workbook has unique headers");
    Assert(sheet.Cells[2, headers.IndexOf("AccountNo") + 1].Text == "001234567890", "Bank export preserves account-number leading zeros");
    Assert(sheet.Cells[2, headers.IndexOf("Branch") + 1].Text == "Main", "Bank export includes branch");
}
var customerController = new CustomerController(db, new CustomerRepo(db), null!, null!, null!);
var customerResult = await customerController.Export(customer.CustomerId.ToString(), default);
Assert(customerResult is FileContentResult, "Customer export succeeds");
using (var package = new ExcelPackage(new MemoryStream(((FileContentResult)customerResult).FileContents)))
{
    var sheet = package.Workbook.Worksheets[0];
    var headers = Enumerable.Range(1, sheet.Dimension.Columns).Select(c => sheet.Cells[1, c].Text).ToList();
    Assert(sheet.Cells[2, headers.IndexOf("VatType") + 1].Text == "Exempt", "Customer export includes separate VAT classification");
    Assert(!headers.Contains("Type"), "Customer export omits removed document classification");
    Assert(sheet.Cells[2, headers.IndexOf("CwtPercent") + 1].GetValue<decimal>() == 0.005m, "Customer export preserves fractional withholding rate");
}
Console.WriteLine("Master file persistence, relationships, code generation, and export checks passed.");
var storedBank = await db.BankAccounts.SingleAsync();
storedBank.IsActive = false;
await db.SaveChangesAsync();
Assert((await new GeneralRepo(db, null!).GetBankAccountListAsync()).Count == 0,
    "Inactive banks are excluded from payment selection");
Assert(await db.BankAccounts.AnyAsync(b => b.BankAccountId == bank.BankAccountId),
    "Inactive banks remain available to historical transactions");
var product = new Product { ProductName = "Fuel", ProductCode = "TEST", ProductUnit = "L" };
db.Products.Add(product);
var invoice = new SalesInvoice
{
    Customer = loaded, Product = product, OtherRefNo = "Test", Remarks = "Rate snapshot",
    CwtPercent = loaded.CwtPercent, CwVatPercent = loaded.CwVatPercent
};
db.SalesInvoices.Add(invoice);
await db.SaveChangesAsync();
loaded.CwtPercent = 0.1m;
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Assert((await db.SalesInvoices.SingleAsync()).CwtPercent == 0.005m,
    "Changing the customer master does not change the invoice's captured withholding rate");
Console.WriteLine("Active-bank selection and invoice withholding snapshot checks passed.");
static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
sealed class CheckDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties()) property.SetColumnType(null);
    }
}
