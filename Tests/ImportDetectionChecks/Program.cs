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
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
var services = new ServiceCollection().AddLogging();
services.AddScoped<ApplicationDbContext>(_ => new CheckDb(options));
services.AddScoped<AasDbContext>(_ => new CheckAasDb(new DbContextOptionsBuilder<AasDbContext>().UseSqlite(connection).Options));
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

ExcelPackage Reorder(ExcelPackage workbook)
{
    var reordered = new ExcelPackage();
    foreach (var original in workbook.Workbook.Worksheets)
    {
        var sheet = reordered.Workbook.Worksheets.Add(original.Name);
        sheet.Cells[1, 1].Value = "Ignored extra column";
        for (var row = 1; row <= original.Dimension.Rows; row++)
            for (var column = 1; column <= original.Dimension.Columns; column++)
                sheet.Cells[row, original.Dimension.Columns - column + 2].Value = row == 1 && original.Cells[row, column].Value is string heading
                    ? " " + heading.ToUpperInvariant() + " " : original.Cells[row, column].Value;
    }
    return reordered;
}

void Assert(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine($"PASS {description}");
}

ExcelWorksheet Sheet(ExcelPackage workbook, string name, int columns = 40)
{
    var sheet = workbook.Workbook.Worksheets.Add(name);
    for (var i = 1; i <= columns; i++) sheet.Cells[1, i].Value = $"Column{i}";
    (int Column, string Header)[] headers = name switch
    {
        "SalesInvoice" => [(1, "OtherRefNo"), (2, "Quantity"), (3, "UnitPrice"), (4, "Amount"), (8, "Remarks"), (9, "Status"), (10, "TransactionDate"), (11, "Discount"), (19, "DueDate"), (20, "CreatedBy"), (21, "CreatedDate"), (22, "CancellationRemarks"), (24, "OriginalCustomerId"), (26, "OriginalProductId"), (27, "OriginalSINo"), (28, "OriginalDocumentId"), (29, "PostedBy"), (30, "PostedDate"), (31, "EditedBy"), (32, "EditedDate"), (33, "CanceledBy"), (34, "CanceledDate"), (35, "VoidedBy"), (36, "VoidedDate")],
        "ServiceInvoice" => [(1, "DueDate"), (2, "Period"), (3, "Amount"), (4, "Total"), (5, "Discount"), (6, "CurrentAndPreviousAmount"), (7, "UnearnedAmount"), (8, "Status"), (11, "Instructions"), (13, "CreatedBy"), (14, "CreatedDate"), (15, "CancellationRemarks"), (16, "OriginalCustomerId"), (17, "OriginalSVNo"), (18, "OriginalServicesId"), (19, "OriginalDocumentId"), (20, "PostedBy"), (21, "PostedDate"), (22, "EditedBy"), (23, "EditedDate"), (24, "CanceledBy"), (25, "CanceledDate"), (26, "VoidedBy"), (27, "VoidedDate")],
        "CollectionReceipt" => [(1, "TransactionDate"), (2, "ReferenceNo"), (3, "Remarks"), (4, "CashAmount"), (5, "CheckDate"), (6, "CheckNo"), (7, "CheckBank"), (8, "CheckBranch"), (9, "CheckAmount"), (10, "ManagerCheckDate"), (11, "ManagerCheckNo"), (12, "ManagerCheckBank"), (13, "ManagerCheckBranch"), (14, "ManagerCheckAmount"), (15, "EWT"), (16, "WVAT"), (17, "Total"), (18, "IsCertificateUpload"), (19, "F2306FilePath"), (20, "F2307FilePath"), (21, "CreatedBy"), (22, "CreatedDate"), (23, "CancellationRemarks"), (24, "MultipleSI"), (25, "MultipleSIId"), (26, "SIMultipleAmount"), (27, "MultipleTransactionDate"), (28, "OriginalCustomerId"), (29, "OriginalSalesInvoiceId"), (30, "OriginalCRNo"), (31, "OriginalServiceInvoiceId"), (32, "OriginalDocumentId"), (33, "PostedBy"), (34, "PostedDate"), (35, "EditedBy"), (36, "EditedDate"), (37, "CanceledBy"), (38, "CanceledDate"), (39, "VoidedBy"), (40, "VoidedDate")],
        "CreditMemo" => [(1, "TransactionDate"), (2, "CreditAmount"), (3, "Description"), (4, "AdjustedPrice"), (5, "Quantity"), (6, "Source"), (7, "Remarks"), (8, "Period"), (9, "Amount"), (10, "CurrentAndPreviousAmount"), (11, "UnearnedAmount"), (12, "ServicesId"), (13, "CreatedBy"), (14, "CreatedDate"), (15, "CancellationRemarks"), (16, "OriginalSalesInvoiceId"), (17, "OriginalCMNo"), (18, "OriginalServiceInvoiceId"), (19, "OriginalDocumentId"), (20, "PostedBy"), (21, "PostedDate"), (22, "EditedBy"), (23, "EditedDate"), (24, "CanceledBy"), (25, "CanceledDate"), (26, "VoidedBy"), (27, "VoidedDate")],
        "DebitMemo" => [(1, "TransactionDate"), (2, "DebitAmount"), (3, "Description"), (4, "AdjustedPrice"), (5, "Quantity"), (6, "Source"), (7, "Remarks"), (8, "Period"), (9, "Amount"), (10, "CurrentAndPreviousAmount"), (11, "UnearnedAmount"), (12, "ServicesId"), (13, "CreatedBy"), (14, "CreatedDate"), (15, "CancellationRemarks"), (16, "OriginalSalesInvoiceId"), (17, "OriginalDMNo"), (18, "OriginalServiceInvoiceId"), (19, "OriginalDocumentId"), (20, "PostedBy"), (21, "PostedDate"), (22, "EditedBy"), (23, "EditedDate"), (24, "CanceledBy"), (25, "CanceledDate"), (26, "VoidedBy"), (27, "VoidedDate")],
        "CheckVoucherHeader" => [(1, "TransactionDate"), (2, "ReceivingReportNo"), (3, "SalesInvoiceNo"), (4, "PurchaseOrderNo"), (5, "Particulars"), (6, "CheckNo"), (7, "Category"), (8, "Payee"), (9, "CheckDate"), (10, "StartDate"), (11, "EndDate"), (12, "NumberOfMonths"), (13, "NumberOfMonthsCreated"), (14, "LastCreatedDate"), (15, "AmountPerMonth"), (16, "IsComplete"), (17, "AccruedType"), (18, "Reference"), (19, "CreatedBy"), (20, "CreatedDate"), (21, "Total"), (22, "Amount"), (23, "CheckAmount"), (24, "CvType"), (25, "AmountPaid"), (26, "IsPaid"), (27, "CancellationRemarks"), (28, "OriginalBankId"), (29, "OriginalCVNo"), (30, "OriginalSupplierId"), (31, "OriginalDocumentId"), (32, "PostedBy"), (33, "PostedDate"), (34, "EditedBy"), (35, "EditedDate"), (36, "CanceledBy"), (37, "CanceledDate"), (38, "VoidedBy"), (39, "VoidedDate")],
        "CheckVoucherTradePayments" => [(2, "DocumentId"), (4, "CheckVoucherId"), (5, "AmountPaid")],
        "MultipleCheckVoucherPayments" => [(2, "CheckVoucherHeaderPaymentId"), (3, "CheckVoucherHeaderInvoiceId"), (4, "AmountPaid")],
        "CheckVoucherDetails" => [(1, "AccountNo"), (2, "AccountName"), (4, "Debit"), (5, "Credit"), (6, "CVHeaderId"), (7, "OriginalDocumentId"), (8, "Amount"), (9, "AmountPaid"), (10, "SupplierId"), (11, "EwtPercent"), (12, "IsUserSelected"), (13, "IsVatable")],
        "JournalVoucherHeader" => [(1, "TransactionDate"), (2, "Reference"), (3, "Particulars"), (4, "CRNo"), (5, "JVReason"), (6, "CreatedBy"), (7, "CreatedDate"), (8, "CancellationRemarks"), (9, "OriginalCVId"), (10, "OriginalJVNo"), (11, "OriginalDocumentId"), (12, "PostedBy"), (13, "PostedDate"), (14, "EditedBy"), (15, "EditedDate"), (16, "CanceledBy"), (17, "CanceledDate"), (18, "VoidedBy"), (19, "VoidedDate")],
        "JournalVoucherDetails" => [(1, "AccountNo"), (2, "AccountName"), (3, "TransactionNo"), (4, "Debit"), (5, "Credit"), (6, "JVHeaderId"), (7, "OriginalDocumentId")],
        "BankAccount" => [(2, "CreatedBy"), (3, "CreatedDate"), (4, "AccountName"), (6, "Bank"), (7, "OriginalBankId")],
        "Customers" => [(1, "CustomerName"), (2, "CustomerAddress"), (3, "ZipCode"), (4, "CustomerTin"), (5, "BusinessStyle"), (6, "CustomerTerms"), (7, "CustomerType"), (8, "WithHoldingVat"), (9, "WithHoldingTax"), (10, "CreatedBy"), (11, "CreatedDate"), (12, "OriginalCustomerId"), (13, "OriginalCustomerNumber")],
        "Supplier" => [(1, "SupplierName"), (2, "SupplierAddress"), (3, "ZipCode"), (4, "SupplierTin"), (5, "SupplierTerms"), (6, "VatType"), (7, "TaxType"), (8, "ProofOfRegistrationFilePath"), (9, "ReasonOfExemption"), (10, "Validity"), (11, "ValidityDate"), (12, "ProofOfExemptionFilePath"), (13, "CreatedBy"), (14, "CreatedDate"), (15, "Branch"), (16, "Category"), (17, "TradeName"), (18, "DefaultExpenseNumber"), (19, "WithholdingTaxPercent"), (20, "WithholdingTaxtitle"), (21, "OriginalSupplierId")],
        "Product" => [(1, "ProductCode"), (2, "ProductName"), (3, "ProductUnit"), (4, "CreatedBy"), (5, "CreatedDate"), (6, "OriginalProductId")],
        "Services" => [(1, "CurrentAndPreviousTitle"), (2, "UnearnedTitle"), (3, "Name"), (4, "Percent"), (5, "CreatedBy"), (6, "CreatedDate"), (7, "CurrentAndPreviousNo"), (8, "UnearnedNo"), (9, "OriginalServiceId")],
        _ => []
    };
    foreach (var (column, header) in headers)
        if (column <= columns) sheet.Cells[1, column].Value = (name, header) switch
        {
            ("Customers", "ZipCode") => "CustomerZipCode",
            ("Customers", "CustomerTin") => "CustomerTinNumber",
            ("Customers", "CustomerTerms") => "Terms",
            ("Supplier", "SupplierName") => "Name",
            ("Supplier", "SupplierAddress") => "Address",
            ("Supplier", "SupplierTin") => "TinNo",
            ("Supplier", "SupplierTerms") => "Terms",
            ("Services", "UnearnedTitle") => "UneranedTitle",
            ("ServiceInvoice", "CurrentAndPreviousAmount") => "CurrentAndPreviousMonth",
            ("CreditMemo", "CreditAmount") => "DebitAmount",
            _ => header
        };
    return sheet;
}

void Set(ExcelWorksheet sheet, params (int Column, object? Value)[] values)
{
    foreach (var (column, value) in values) sheet.Cells[2, column].Value = value;
}

ExcelWorksheet SI(ExcelPackage workbook)
{
    var sheet = Sheet(workbook, "SalesInvoice");
    Set(sheet, (1, "Ref"), (2, 10m), (3, 11.2m), (4, 112m), (8, "Sales"), (9, "Pending"), (10, date), (19, date),
        (20, "import-check"), (21, date), (24, 101), (26, 201), (27, "SI0001"), (28, 601), (29, "import-check"), (30, date));
    return sheet;
}

ExcelWorksheet SV(ExcelPackage workbook)
{
    var sheet = Sheet(workbook, "ServiceInvoice");
    Set(sheet, (1, date), (2, date), (3, 100m), (4, 112m), (8, "Pending"), (11, "Service"), (13, "import-check"),
        (14, date), (16, 101), (17, "SV0001"), (18, 301), (19, 701), (20, "import-check"), (21, date));
    return sheet;
}

ExcelWorksheet Receipt(ExcelPackage workbook, bool service = false)
{
    var sheet = Sheet(workbook, "CollectionReceipt");
    Set(sheet, (1, date), (2, "Ref"), (3, "Receipt"), (4, 56m), (17, 56m), (21, "import-check"), (22, date),
        (28, 101), (29, service ? 0 : 601), (30, service ? "CR0002" : "CR0001"), (31, service ? 701 : 0),
        (32, service ? 802 : 801), (33, "import-check"), (34, date));
    return sheet;
}

ExcelWorksheet PO(ExcelPackage workbook)
{
    var sheet = Sheet(workbook, "PurchaseOrder");
    var headers = new[] { "Date", "Terms", "Quantity", "Price", "Amount", "FinalPrice", "QuantityReceived", "IsReceived", "ReceivedDate", "Remarks", "CreatedBy", "CreatedDate", "IsClosed", "CancellationRemarks", "OriginalProductId", "OriginalPONo", "OriginalSupplierId", "OriginalDocumentId", "PostedBy", "PostedDate", "EditedBy", "EditedDate", "CanceledBy", "CanceledDate", "VoidedBy", "VoidedDate" };
    for (var column = 0; column < headers.Length; column++) sheet.Cells[1, column + 1].Value = headers[column];
    Set(sheet, (1, date), (2, "COD"), (3, 50m), (4, 11.2m), (5, 560m), (10, "Purchase"), (11, "import-check"),
        (12, date), (15, 201), (16, "PO0001"), (17, 401), (18, 1201), (19, "import-check"), (20, date));
    return sheet;
}

ExcelWorksheet RR(ExcelPackage workbook)
{
    var sheet = Sheet(workbook, "ReceivingReport");
    var headers = new[] { "Date", "DueDate", "SupplierInvoiceNumber", "SupplierInvoiceDate", "TruckOrVessels", "QuantityDelivered", "QuantityReceived", "GainOrLoss", "Amount", "AmountPaid", "IsPaid", "PaidDate", "OtherRef", "Remarks", "CanceledQuantity", "Legacy16", "Legacy17", "Legacy18", "CreatedBy", "CreatedDate", "CancellationRemarks", "ReceivedDate", "OriginalPOId", "OriginalRRNo", "OriginalDocumentId", "PostedBy", "PostedDate", "EditedBy", "EditedDate", "CanceledBy", "CanceledDate", "VoidedBy", "VoidedDate" };
    for (var column = 0; column < headers.Length; column++) sheet.Cells[1, column + 1].Value = headers[column];
    Set(sheet, (1, date), (2, date), (3, "Supplier invoice"), (5, "Truck"), (6, 50m), (7, 50m), (9, 560m),
        (14, "Receipt of goods"), (19, "import-check"), (20, date), (22, date), (23, 1201), (24, "RR0001"),
        (25, 1101), (26, "import-check"), (27, date));
    return sheet;
}

ExcelWorksheet CV(ExcelPackage workbook)
{
    var sheet = Sheet(workbook, "CheckVoucherHeader");
    Set(sheet, (1, date), (2, "RR0001"), (4, "PO0001"), (5, "Payment"), (7, "Trade"), (8, "Supplier"),
        (9, date), (10, date), (11, date), (19, "import-check"), (20, date), (21, 56m), (23, 56m),
        (24, "Payment"), (28, 501), (29, "CVSOURCE1"), (30, 401), (31, 901), (32, "import-check"), (33, date));
    var details = Sheet(workbook, "CheckVoucherDetails");
    Set(details, (1, "202010100"), (2, "AP"), (4, 56m), (6, 901), (7, 1501), (10, 401));
    details.Cells[2, 1, 2, 13].Copy(details.Cells[3, 1, 3, 13]);
    details.Cells[3, 1].Value = "101010100"; details.Cells[3, 4].Value = 0m;
    details.Cells[3, 5].Value = 56m; details.Cells[3, 7].Value = 1502;
    return sheet;
}


object Sample(Type type, bool changed = false, string field = "")
{
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type.IsArray)
    {
        var array = Array.CreateInstance(type.GetElementType()!, 1);
        array.SetValue(Sample(type.GetElementType()!, changed, field), 0);
        return array;
    }
    if (type == typeof(string)) return field.Contains("Date") ? (changed ? "2026-01-16" : "2026-01-15") : changed ? "changed" : "same";
    if (type == typeof(decimal)) return changed ? 12.34m : 1.23m;
    if (type == typeof(int)) return changed ? 8 : 7;
    if (type == typeof(bool)) return !changed;
    if (type == typeof(DateTime)) return date.AddDays(changed ? 1 : 0);
    if (type == typeof(DateOnly)) return DateOnly.FromDateTime(date.AddDays(changed ? 1 : 0));
    throw new Exception("Unsupported sample type " + type);
}
var detectedFields = 0;
foreach (var (repoType, sheetName) in new[] {
    (typeof(SalesInvoiceRepo), "SalesInvoice"), (typeof(ServiceInvoiceRepo), "ServiceInvoice"),
    (typeof(PurchaseOrderRepo), "PurchaseOrder"), (typeof(ReceivingReportRepo), "ReceivingReport"),
    (typeof(ReceiptRepo), "CollectionReceipt"), (typeof(CreditMemoRepo), "CreditMemo"),
    (typeof(DebitMemoRepo), "DebitMemo"), (typeof(CheckVoucherRepo), "CheckVoucherHeader"),
    (typeof(JournalVoucherRepo), "JournalVoucherHeader") })
{
    using var workbook = new ExcelPackage();
    var sheet = sheetName switch {
        "SalesInvoice" => SI(workbook), "ServiceInvoice" => SV(workbook),
        "PurchaseOrder" => PO(workbook), "ReceivingReport" => RR(workbook),
        "CollectionReceipt" => Receipt(workbook), "CheckVoucherHeader" => CV(workbook),
        _ => Sheet(workbook, sheetName)
    };
    for (var column = 1; column <= sheet.Dimension.Columns; column++)
        if (sheet.Cells[1, column].Text is "Date" or "TransactionDate" or "DueDate" or "Period" or "CreatedDate")
            sheet.Cells[2, column].Value = date;
    var parser = repoType.GetMethods().Single(x => x.Name == "ParseWorksheet");
    foreach (var namesOnly in new[] { true, false })
    {
        for (var column = 1; column <= sheet.Dimension.Columns; column++)
        {
            var heading = sheet.Cells[1, column].Text;
            if (heading is "PostedBy" or "CanceledBy" or "VoidedBy" or "EditedBy")
                sheet.Cells[2, column].Value = namesOnly ? "Import user" : null;
            if (heading is "PostedDate" or "CanceledDate" or "VoidedDate" or "EditedDate")
                sheet.Cells[2, column].Value = namesOnly ? null : date;
        }
        var arguments = parser.GetParameters().Length == 1 ? new object[] { sheet } : new object[] { sheet, CancellationToken.None };
        var result = parser.Invoke(scope.ServiceProvider.GetRequiredService(repoType), arguments)!;
        if (result is Task task) { await task; result = task.GetType().GetProperty("Result")!.GetValue(task)!; }
        var row = ((System.Collections.IEnumerable)result).Cast<object>().Single();
        foreach (var prefix in new[] { "Posted", "Canceled", "Voided", "Edited" })
        {
            Assert((string?)row.GetType().GetProperty(prefix + "By")!.GetValue(row) == (namesOnly ? "Import user" : ""),
                sheetName + "/" + prefix + "By allows an independent blank value");
            Assert((DateTime)row.GetType().GetProperty(prefix + "Date")!.GetValue(row)! == (namesOnly ? default : date),
                sheetName + "/" + prefix + "Date allows an independent blank value");
        }
    }
}
foreach (var repoType in new[] { typeof(SalesInvoiceRepo), typeof(ServiceInvoiceRepo), typeof(PurchaseOrderRepo), typeof(ReceivingReportRepo),
    typeof(ReceiptRepo), typeof(CreditMemoRepo), typeof(DebitMemoRepo), typeof(CheckVoucherRepo), typeof(JournalVoucherRepo) })
foreach (var method in repoType.GetMethods().Where(x => x.Name is "Detect" or "DetectCvDetails" or "DetectJvDetails"))
{
    var repo = scope.ServiceProvider.GetRequiredService(repoType);
    var entity = Activator.CreateInstance(method.GetParameters()[0].ParameterType)!;
    var row = Activator.CreateInstance(method.GetParameters()[1].ParameterType)!;
    var common = row.GetType().GetProperties().Where(x => entity.GetType().GetProperty(x.Name) != null &&
        x.Name != "CheckVoucherHeaderNo" && x.Name != "SalesInvoiceId" && x.Name != "ServiceInvoiceId").ToList(); // This local generated number is not an Excel input.
    foreach (var property in common)
    {
        property.SetValue(row, Sample(property.PropertyType, field: property.Name));
        entity.GetType().GetProperty(property.Name)!.SetValue(entity, Sample(property.PropertyType, field: property.Name));
    }
    // Header IDs have different local/source names, and must be projected through their navigation.
    if (entity is CheckVoucherDetail cvDetail)
    {
        cvDetail.CheckVoucherHeader = new CheckVoucherHeader { OriginalDocumentId = 901, OriginalSeriesNumber = "same" };
        cvDetail.Supplier = new Supplier { OriginalSupplierId = 401 };
        row.GetType().GetProperty("CvHeaderId")!.SetValue(row, 901);
        row.GetType().GetProperty("SupplierId")!.SetValue(row, 401);
    }
    if (entity is JournalVoucherDetail jvDetail)
    {
        jvDetail.JournalVoucherHeader = new JournalVoucherHeader { OriginalDocumentId = 7, OriginalSeriesNumber = "same" };
    }
    foreach (var property in common)
    {
        if (property.Name is "AmountPaid" or "Balance" or "PaidDate" or "IsPaid" or "IsTaxAndVatPaid") continue;
        if (entity is PurchaseOrder && property.Name is "QuantityReceived" or "IsReceived" or "ReceivedDate") continue;
        if (entity is CheckVoucherDetail && property.Name == "SupplierId") continue;
        if (entity is JournalVoucherDetail && property.Name == "JournalVoucherHeaderId") continue;
        var before = (Dictionary<string, (string, string)>)method.Invoke(repo, [entity, row, new List<ImportExportLog>()])!;
        Assert(!before.ContainsKey(property.Name), repoType.Name + "/" + method.Name + "/" + property.Name + " equal values");
        property.SetValue(row, Sample(property.PropertyType, true, property.Name));
        var changed = (Dictionary<string, (string, string)>)method.Invoke(repo, [entity, row, new List<ImportExportLog>()])!;
        Assert(changed.ContainsKey(property.Name), repoType.Name + "/" + method.Name + "/" + property.Name + " changed value detected");
        property.SetValue(row, Sample(property.PropertyType, field: property.Name));
        detectedFields++;
    }
}
Console.WriteLine($"Verified {detectedFields} field comparisons across document headers and details.");

async Task<bool> Import(ExcelPackage workbook, string name)
{
    using var reordered = Reorder(workbook);
    return await ActivatorUtilities.CreateInstance<WorkbookImport>(scope.ServiceProvider).ImportAsync(reordered, name, CancellationToken.None, user);
}
using (var workbook = new ExcelPackage())
{
    var po = PO(workbook); RR(workbook);
    await Import(workbook, "ReceivingReport");
    Set(po, (7, 50m), (8, true), (9, date));
    var before = await db.ImportExportLogs.CountAsync();
    await Import(workbook, "ReceivingReport");
    Assert(await db.ImportExportLogs.CountAsync() == before, "matching full PO/RR sheets compare against pre-reversal aggregates without false changes");
    var rr = workbook.Workbook.Worksheets["ReceivingReport"];
    Set(rr, (28, "Editor"), (29, date), (4, "2026-01-16"));
    await Import(workbook, "ReceivingReport");
    Assert(await db.ImportExportLogs.AnyAsync(x => x.TableName == "ReceivingReport" && x.ColumnName == "EditedBy" && x.AdjustedValue == "Editor"), "embedded RR edit user is logged");
    Assert(await db.ImportExportLogs.AnyAsync(x => x.TableName == "ReceivingReport" && x.ColumnName == "SupplierInvoiceDate" && x.AdjustedValue == "2026-01-16"), "RR supplier date compares Excel rather than database to itself");
}
using (var workbook = new ExcelPackage())
{
    CV(workbook);
    var trade = Sheet(workbook, "CheckVoucherTradePayments");
    Set(trade, (2, 1101), (4, 901), (5, 12.34m));
    await Import(workbook, "CheckVoucherHeader");
    var before = await db.ImportExportLogs.CountAsync();
    await Import(workbook, "CheckVoucherHeader");
    Assert(await db.ImportExportLogs.CountAsync() == before, "payment source IDs match local relationships on unchanged reimport");
    Set(trade, (5, 23.45m));
    await Import(workbook, "CheckVoucherHeader");
    Assert(await db.ImportExportLogs.AnyAsync(x => x.ColumnName == "CheckVoucherTradePayments.AmountPaid" && x.AdjustedValue == "23.4500"), "payment amount changes are logged before replacement");
    trade.Cells[2, 1, trade.Dimension.Rows, trade.Dimension.Columns].Clear();
    await Import(workbook, "CheckVoucherHeader");
    Assert(await db.ImportExportLogs.AnyAsync(x => x.ColumnName == "CheckVoucherTradePayments.DocumentId" && x.AdjustedValue == ""), "omitted payment relationships are logged");
}
foreach (var kind in new[] { "BankAccount", "ChartOfAccount", "Customers", "Supplier", "Product", "Services" })
{
    using var workbook = new ExcelPackage();
    var sheet = Sheet(workbook, kind);
    var nameColumn = 1;
    switch (kind)
    {
        case "BankAccount": nameColumn = 4; Set(sheet, (2, "import-check"), (3, date), (4, "Original"), (6, "Bank"), (7, 9001)); break;
        case "ChartOfAccount":
            nameColumn = 3;
            var headers = new[] { "IsMain", "AccountNumber", "AccountName", "AccountType", "NormalBalance", "Level", "CreatedBy", "CreatedDate", "EditedBy", "EditedDate", "HasChildren", "ParentAccountId", "OriginalChartOfAccount" };
            for (var column = 0; column < headers.Length; column++) sheet.Cells[1, column + 1].Value = headers[column];
            Set(sheet, (1, false), (2, "9990001"), (3, "Original"), (4, "Asset"), (5, "Debit"), (6, 1), (7, "import-check"), (8, date), (11, false), (13, 9001));
            break;
        case "Customers": Set(sheet, (1, "Original"), (2, "Address"), (4, "TIN"), (5, "Trade"), (6, "COD"), (7, "Vatable"), (10, "import-check"), (11, date), (12, 9001)); break;
        case "Supplier": Set(sheet, (1, "Original"), (2, "Address"), (4, "TIN"), (5, "COD"), (6, "Vatable"), (7, "Without Tax"), (13, "import-check"), (14, date), (15, "Main"), (16, "Trade"), (21, 9001)); break;
        case "Product": nameColumn = 2; Set(sheet, (1, "TEST001"), (2, "Original"), (3, "L"), (4, "import-check"), (5, date), (6, 9001)); break;
        case "Services": nameColumn = 3; Set(sheet, (1, "Revenue"), (2, "Unearned"), (3, "Original"), (4, 1), (5, "import-check"), (6, date), (7, "401020100"), (8, "201000100"), (9, 9001)); break;
    }
    await Import(workbook, kind);
    var unchangedLogs = await db.ImportExportLogs.CountAsync();
    await Import(workbook, kind);
    Assert(await db.ImportExportLogs.CountAsync() == unchangedLogs, kind + " matching master workbook has no new logs");
    sheet.Cells[2, nameColumn].Value = "Updated";
    await Import(workbook, kind);
    Assert(await db.ImportExportLogs.AnyAsync(x => x.AdjustedValue == "Updated" && x.TableName == (kind == "Customers" ? "Customer" : kind == "Services" ? "Service" : kind)),
        $"{kind} updates existing master record and logs change");
}

var cvRepo = scope.ServiceProvider.GetRequiredService<CheckVoucherRepo>();
var tradeBefore = new[] { new Accounting_System.Models.ViewModels.CvTradePaymentUploadExcelFileViewModel { DocumentId = 1101, DocumentType = "RR", CheckVoucherId = 901, AmountPaid = 12.34m } };
foreach (var field in new[] { "DocumentId", "DocumentType", "CheckVoucherId", "AmountPaid" })
{
    var after = new Accounting_System.Models.ViewModels.CvTradePaymentUploadExcelFileViewModel { DocumentId = 1101, DocumentType = "RR", CheckVoucherId = 901, AmountPaid = 12.34m };
    var property = after.GetType().GetProperty(field)!;
    property.SetValue(after, field == "AmountPaid" ? 33.45m : Sample(property.PropertyType, true, field));
    Assert(cvRepo.DetectTradePayments(tradeBefore, [after]).ContainsKey("CheckVoucherTradePayments." + field), "trade payment " + field + " comparison");
}
var multipleBefore = new[] { new Accounting_System.Models.ViewModels.CvMultiplePaymentUploadExcelFileViewModel { CheckVoucherHeaderPaymentId = 901, CheckVoucherHeaderInvoiceId = 902, AmountPaid = 12.34m } };
foreach (var field in new[] { "CheckVoucherHeaderPaymentId", "CheckVoucherHeaderInvoiceId", "AmountPaid" })
{
    var after = new Accounting_System.Models.ViewModels.CvMultiplePaymentUploadExcelFileViewModel { CheckVoucherHeaderPaymentId = 901, CheckVoucherHeaderInvoiceId = 902, AmountPaid = 12.34m };
    var property = after.GetType().GetProperty(field)!;
    property.SetValue(after, field == "AmountPaid" ? 33.45m : Sample(property.PropertyType, true, field));
    Assert(cvRepo.DetectMultiplePayments(multipleBefore, [after]).ContainsKey("MultipleCheckVoucherPayments." + field), "multiple payment " + field + " comparison");
}
using (var workbook = new ExcelPackage())
{
    var sheet = Sheet(workbook, "MultipleCheckVoucherPayments");
    Set(sheet, (2, 901), (3, 902), (4, 12.34m));
    Assert(cvRepo.ParseWorksheetCvMultiplePayment(sheet).Single().AmountPaid == 12.34m, "multiple-payment parser retains fractional amounts");
}
using (var workbook = new ExcelPackage())
{
    SI(workbook);
    var receipt = Receipt(workbook);
    Set(receipt, (4, 11.2m), (17, 11.2m), (24, "SI0001"), (25, "601"), (26, "11.20"), (29, 0));
    await Import(workbook, "CollectionReceipt");
    var before = await db.ImportExportLogs.CountAsync();
    await Import(workbook, "CollectionReceipt");
    Assert(await db.ImportExportLogs.CountAsync() == before, "receipt arrays compare values and project local invoice IDs to source IDs");
    Set(receipt, (4, 22.4m), (17, 22.4m), (26, "22.40"));
    await Import(workbook, "CollectionReceipt");
    Assert(await db.ImportExportLogs.AnyAsync(x => x.TableName == "CollectionReceipt" && x.ColumnName == "SIMultipleAmount" && x.AdjustedValue == "22.4000"), "changed receipt allocation array is logged");
}
{
    var repo = scope.ServiceProvider.GetRequiredService<SalesInvoiceRepo>();
    var entity = new SalesInvoice { PostedDate = date.AddHours(1) };
    var row = new Accounting_System.Models.ViewModels.SalesInvoiceUploadExcelFileViewModel { PostedDate = date.AddHours(13) };
    Assert(repo.Detect(entity, row, []).ContainsKey("PostedDate"), "timestamps distinguish AM and PM");
    row.PostedDate = date.AddHours(1).AddTicks(1);
    Assert(repo.Detect(entity, row, []).ContainsKey("PostedDate"), "timestamps retain fractional-second precision");
    entity.PostedDate = null; row.PostedDate = default;
    Assert(!repo.Detect(entity, row, []).ContainsKey("PostedDate"), "null and blank optional posted dates are equivalent");
}
{
    var aas = scope.ServiceProvider.GetRequiredService<AasDbContext>();
    var bank = await aas.BankAccounts.FirstAsync(x => x.OriginalBankId == 501);
    var uploaded = (BankAccount)aas.Entry(bank).CurrentValues.ToObject();
    uploaded.AccountName = "Changed AAS bank";
    var general = scope.ServiceProvider.GetRequiredService<GeneralRepo>();
    general.LogMasterComparison(aas, bank, uploaded, "BankAccount", "import-check");
    Assert(await general.SaveImportComparisonsAsync(CancellationToken.None), "AAS master comparisons produce review notifications");
    Assert(await db.ImportExportLogs.AnyAsync(x => x.DatabaseName == "AAS" && x.ColumnName == "AccountName" && x.AdjustedValue == "Changed AAS bank"), "AAS master changed fields are logged");
    Assert((await aas.BankAccounts.AsNoTracking().FirstAsync(x => x.OriginalBankId == 501)).AccountName == "Account", "AAS comparison leaves existing master values unchanged");
}

using (var workbook = new ExcelPackage())
{
    var aas = scope.ServiceProvider.GetRequiredService<AasDbContext>();
    var header = await aas.CheckVoucherHeaders.SingleAsync(x => x.OriginalDocumentId == 901);
    var report = await aas.ReceivingReports.SingleAsync(x => x.OriginalDocumentId == 1101);
    aas.CVTradePayments.Add(new CVTradePayment { CheckVoucherId = header.CheckVoucherHeaderId, DocumentId = report.ReceivingReportId, DocumentType = "RR", AmountPaid = 1.23m });
    await aas.SaveChangesAsync();
    var cv = CV(workbook);
    var trade = Sheet(workbook, "CheckVoucherTradePayments");
    Set(trade, (2, 1101), (4, 901), (5, 12.34m));
    await cvRepo.DetectPaymentWorksheetsAsync(cv, trade, null, "import-check", true, CancellationToken.None);
    await scope.ServiceProvider.GetRequiredService<GeneralRepo>().SaveImportComparisonsAsync(CancellationToken.None);
    Assert(await db.ImportExportLogs.AnyAsync(x => x.DatabaseName == "AAS" && x.ColumnName == "CheckVoucherTradePayments.AmountPaid" && x.OriginalValue == "1.2300" && x.AdjustedValue == "12.3400"),
        "AAS payment sheets compare source identities and amounts against AAS records");
}
foreach (var kind in new[] { "BankAccount", "ChartOfAccount", "Customers", "Supplier", "Product", "Services" })
{
    using var workbook = new ExcelPackage();
    var sheet = Sheet(workbook, kind);
    var nameColumn = 1;
    switch (kind)
    {
        case "BankAccount": nameColumn = 4; Set(sheet, (2, "import-check"), (3, date), (4, "Updated"), (6, "Bank"), (7, 9001)); break;
        case "ChartOfAccount":
            nameColumn = 3;
            var headers = new[] { "IsMain", "AccountNumber", "AccountName", "AccountType", "NormalBalance", "Level", "CreatedBy", "CreatedDate", "EditedBy", "EditedDate", "HasChildren", "ParentAccountId", "OriginalChartOfAccount" };
            for (var column = 0; column < headers.Length; column++) sheet.Cells[1, column + 1].Value = headers[column];
            Set(sheet, (1, false), (2, "9990001"), (3, "Updated"), (4, "Asset"), (5, "Debit"), (6, 1), (7, "import-check"), (8, date), (11, false), (13, 9001));
            break;
        case "Customers": Set(sheet, (1, "Updated"), (2, "Address"), (4, "TIN"), (5, "Trade"), (6, "COD"), (7, "Vatable"), (10, "import-check"), (11, date), (12, 9001)); break;
        case "Supplier": Set(sheet, (1, "Updated"), (2, "Address"), (4, "TIN"), (5, "COD"), (6, "Vatable"), (7, "Without Tax"), (13, "import-check"), (14, date), (15, "Main"), (16, "Trade"), (21, 9001)); break;
        case "Product": nameColumn = 2; Set(sheet, (1, "TEST001"), (2, "Updated"), (3, "L"), (4, "import-check"), (5, date), (6, 9001)); break;
        case "Services": nameColumn = 3; Set(sheet, (1, "Revenue"), (2, "Unearned"), (3, "Updated"), (4, 1), (5, "import-check"), (6, date), (7, "401020100"), (8, "201000100"), (9, 9001)); break;
    }
    var controllerName = kind == "Customers" ? "Customer" : kind == "Services" ? "Service" : kind;
    var controllerType = typeof(Accounting_System.Controllers.SalesInvoiceController).Assembly.GetType("Accounting_System.Controllers." + controllerName + "Controller")!;
    var controller = (Microsoft.AspNetCore.Mvc.Controller)ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
    var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user, RequestServices = scope.ServiceProvider };
    controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http, RouteData = new Microsoft.AspNetCore.Routing.RouteData(), ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor() };
    controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
    controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new TestTempData());
    async Task Upload()
    {
        using var reordered = Reorder(workbook);
        using var stream = new MemoryStream(await reordered.GetAsByteArrayAsync());
        var file = new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "file", "checks-" + Accounting_System.Utility.CS.Name + ".xlsx");
        await (Task<Microsoft.AspNetCore.Mvc.IActionResult>)controllerType.GetMethod("AasImport")!.Invoke(controller, [file, CancellationToken.None])!;
        Assert(!controller.TempData.ContainsKey("error"), kind + " AAS action succeeds: " + controller.TempData["error"]);
    }
    var before = await db.ImportExportLogs.CountAsync();
    await Upload();
    Assert(await db.ImportExportLogs.CountAsync() == before, kind + " AAS reordered matching workbook has no differences");
    sheet.Cells[2, nameColumn].Value = "Changed AAS action";
    await Upload();
    Assert(await db.ImportExportLogs.AnyAsync(x => x.DatabaseName == "AAS" && x.TableName == controllerName && x.AdjustedValue.ToLower() == "changed aas action"),
        kind + " AAS action detects changed fields");
    var pendingCount = await db.ImportExportLogs.CountAsync();
    controller.TempData.Remove("importChanges");
    await Upload();
    Assert(await db.ImportExportLogs.CountAsync() == pendingCount, kind + " AAS unchanged reupload does not duplicate pending proposals");
    Assert(!controller.TempData.ContainsKey("importChanges"), kind + " AAS repeated pending proposal does not report new changes");
    sheet.Cells[2, nameColumn].Value = "Another AAS change";
    await Upload();
    Assert(await db.ImportExportLogs.CountAsync() > pendingCount, kind + " AAS genuinely different proposal is still logged");
}


{
    var detail = new CheckVoucherDetail { CheckVoucherHeader = new CheckVoucherHeader { OriginalDocumentId = 901, OriginalSeriesNumber = "CVSOURCE1" }, Supplier = new Supplier { OriginalSupplierId = 401 } };
    var row = new Accounting_System.Models.ViewModels.CheckVoucherDetailsUploadExcelFileViewModel { CvHeaderId = 902, SupplierId = 402, TransactionNo = "CVSOURCE2" };
    var changes = cvRepo.DetectCvDetails(detail, row, []);
    Assert(changes.ContainsKey("SupplierId") && changes.ContainsKey("CheckVoucherHeaderId") && changes.ContainsKey("TransactionNo"), "CV detail source references and supplier changes are detected");
    var jv = scope.ServiceProvider.GetRequiredService<JournalVoucherRepo>();
    var jvDetail = new JournalVoucherDetail { JournalVoucherHeader = new JournalVoucherHeader { OriginalDocumentId = 701, OriginalSeriesNumber = "JV1" } };
    var jvRow = new Accounting_System.Models.ViewModels.JournalVoucherDetailsUploadExcelFileViewModel { JournalVoucherHeaderId = 702, TransactionNo = "JV2" };
    changes = jv.DetectJvDetails(jvDetail, jvRow, []);
    Assert(changes.ContainsKey("JournalVoucherHeaderId") && changes.ContainsKey("TransactionNo"), "JV detail source references are detected");
}
using (var workbook = new ExcelPackage())
{
    CV(workbook);
    var details = workbook.Workbook.Worksheets["CheckVoucherDetails"];
    details.Cells[2, 8].Value = 12.34m;
    await Import(workbook, "CheckVoucherHeader");
    var logs = await db.ImportExportLogs.CountAsync();
    for (var column = 8; column <= 13; column++) details.Cells[1, column].Value = "Ignored" + column;
    await Import(workbook, "CheckVoucherHeader");
    Assert(await db.ImportExportLogs.CountAsync() == logs, "absent optional CV detail headings do not create differences");
    var saved = await db.CheckVoucherDetails.SingleAsync(x => x.OriginalDocumentId == 1501);
    Assert(saved.Amount == 12.34m && saved.SupplierId != null, "older seven-column detail upload retains values absent from Excel");
}

using (var workbook = new ExcelPackage())
{
    var si = SI(workbook); var sv = SV(workbook); Receipt(workbook);
    foreach (var (column, heading, value) in new (int, string, object)[] { (5, "AmountPaid", 9999m), (6, "Balance", 8888m), (7, "IsPaid", true), (12, "IsTaxAndVatPaid", true) })
    { si.Cells[1, column].Value = heading; si.Cells[2, column].Value = value; }
    foreach (var (column, heading, value) in new (int, string, object)[] { (9, "AmountPaid", 9999m), (10, "Balance", 8888m), (12, "IsPaid", true) })
    { sv.Cells[1, column].Value = heading; sv.Cells[2, column].Value = value; }
    var before = await db.ImportExportLogs.Select(x => x.Id).ToListAsync();
    await Import(workbook, "CollectionReceipt");
    await Import(workbook, "CollectionReceipt");
    Assert(!await db.ImportExportLogs.AnyAsync(x => !before.Contains(x.Id) && (x.TableName == "SalesInvoice" || x.TableName == "ServiceInvoice") &&
        (x.ColumnName == "AmountPaid" || x.ColumnName == "Balance" || x.ColumnName == "IsPaid" || x.ColumnName == "IsTaxAndVatPaid")),
        "SI/SV calculated payment fields do not create upload differences even with stale Excel values");
}
using (var workbook = new ExcelPackage())
{
    var po = PO(workbook); var rr = RR(workbook);
    Set(po, (7, 9999m), (8, true), (9, "not a received date"));
    Set(rr, (10, 9999m), (11, true), (12, "not a paid date"));
    var before = await db.ImportExportLogs.Select(x => x.Id).ToListAsync();
    await Import(workbook, "ReceivingReport");
    await Import(workbook, "ReceivingReport");
    Assert(!await db.ImportExportLogs.AnyAsync(x => !before.Contains(x.Id) &&
        ((x.TableName == "PurchaseOrder" && (x.ColumnName == "QuantityReceived" || x.ColumnName == "IsReceived" || x.ColumnName == "ReceivedDate")) ||
         (x.TableName == "ReceivingReport" && (x.ColumnName == "AmountPaid" || x.ColumnName == "IsPaid" || x.ColumnName == "PaidDate")))),
        "calculated PO receipt totals and RR payment fields are ignored by parsing and detection");
}
{
    var header = new CheckVoucherHeader { AmountPaid = 10m, IsPaid = false };
    var uploaded = new Accounting_System.Models.ViewModels.CheckVoucherUploadExcelFileViewModel
    { AmountPaid = 99m, IsPaid = true, RRNo = [], SINo = [], PONo = [], Amount = [] };
    var changes = cvRepo.Detect(header, uploaded, []);
    Assert(!changes.ContainsKey("AmountPaid") && !changes.ContainsKey("IsPaid"), "CV header calculated payment fields do not create differences");
    var details = new CheckVoucherDetail { AmountPaid = 10m };
    var row = new Accounting_System.Models.ViewModels.CheckVoucherDetailsUploadExcelFileViewModel { AmountPaid = 99m };
    Assert(!cvRepo.DetectCvDetails(details, row, []).ContainsKey("AmountPaid"), "CV detail AmountPaid is not compared");
}

foreach (var (name, controllerName) in new[] { ("SalesInvoice", "SalesInvoice"), ("ServiceInvoice", "ServiceInvoice"),
    ("PurchaseOrder", "PurchaseOrder"), ("ReceivingReport", "ReceivingReport"), ("CollectionReceipt", "Receipt"),
    ("CreditMemo", "CreditMemo"), ("DebitMemo", "DebitMemo"), ("CheckVoucherHeader", "CheckVoucherTrade"),
    ("JournalVoucherHeader", "JournalVoucher") })
{
    using var workbook = new ExcelPackage();
    var sheet = name switch { "SalesInvoice" => SI(workbook), "ServiceInvoice" => SV(workbook),
        "PurchaseOrder" => PO(workbook), "ReceivingReport" => RR(workbook), "CollectionReceipt" => Receipt(workbook),
        "CheckVoucherHeader" => CV(workbook), _ => Sheet(workbook, name) };
    const int sourceId = 990001;
    for (var column = 1; column <= sheet.Dimension.Columns; column++)
    {
        var heading = sheet.Cells[1, column].Text;
        if (heading is "Date" or "TransactionDate" or "DueDate" or "Period" or "CreatedDate") sheet.Cells[2, column].Value = date;
        if (heading == "OriginalDocumentId") sheet.Cells[2, column].Value = sourceId;
        if (heading is "OriginalSINo" or "OriginalSVNo" or "OriginalPONo" or "OriginalRRNo" or "OriginalCRNo" or
            "OriginalCMNo" or "OriginalDMNo" or "OriginalCVNo" or "OriginalJVNo") sheet.Cells[2, column].Value = "AAS" + controllerName[..2] + "000001";
        if (heading is "PostedBy" or "PostedDate" or "CanceledBy" or "CanceledDate" or "VoidedBy" or "VoidedDate")
            sheet.Cells[2, column].Value = null;
    }
    if (name is "CreditMemo" or "DebitMemo")
        for (var column = 1; column <= sheet.Dimension.Columns; column++)
            if (sheet.Cells[1, column].Text == "ServicesId") sheet.Cells[2, column].Value = (await db.Services.FirstAsync()).ServiceId;
    if (name == "CheckVoucherHeader")
    {
        sheet.Cells[2, 28].Value = 0;
        var details = workbook.Workbook.Worksheets["CheckVoucherDetails"];
        for (var row = 2; row <= details.Dimension.Rows; row++)
        { details.Cells[row, 6].Value = sourceId; details.Cells[row, 7].Value = sourceId + row; }
    }
    if (name == "JournalVoucherHeader")
    {
        var details = Sheet(workbook, "JournalVoucherDetails");
        Set(details, (1, "101010100"), (2, "Cash"), (3, "AASJo000001"), (4, 25m), (6, sourceId), (7, sourceId + 2));
    }
    var controllerType = typeof(Accounting_System.Controllers.SalesInvoiceController).Assembly.GetType("Accounting_System.Controllers." + controllerName + "Controller")!;
    var controller = (Microsoft.AspNetCore.Mvc.Controller)ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
    var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user, RequestServices = scope.ServiceProvider };
    controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http, RouteData = new Microsoft.AspNetCore.Routing.RouteData(), ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor() };
    controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
    controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new TestTempData());
    for (var attempt = 0; attempt < 2; attempt++)
    {
        controller.TempData.Clear();
        var pendingBeforeUpload = await db.ImportExportLogs.CountAsync(x => x.DatabaseName == "AAS" && x.Action == "");
        using var reordered = Reorder(workbook);
        using var stream = new MemoryStream(await reordered.GetAsByteArrayAsync());
        var file = new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "file", "checks-" + Accounting_System.Utility.CS.Name + ".xlsx");
        await (Task<Microsoft.AspNetCore.Mvc.IActionResult>)controllerType.GetMethod("AasImport")!.Invoke(controller, [file, CancellationToken.None])!;
        Assert(!controller.TempData.ContainsKey("error") && !controller.TempData.ContainsKey("warning"),
            name + " AAS " + (attempt == 0 ? "upload" : "reupload") + " succeeds: " + controller.TempData["error"] + controller.TempData["warning"]);
        var pendingAfterUpload = await db.ImportExportLogs.CountAsync(x => x.DatabaseName == "AAS" && x.Action == "");
        Assert(controller.TempData.ContainsKey("importChanges") == (pendingAfterUpload > pendingBeforeUpload),
            name + " AAS upload notification reflects new differences, not older Product/master logs");
        if (name == "JournalVoucherHeader")
            Assert(!controller.TempData.ContainsKey("importChanges"), "AAS JV without differences ignores existing Product proposals");
    }
    if (name == "JournalVoucherHeader")
    {
        var header = await db.JournalVoucherHeaders.SingleAsync(x => x.OriginalDocumentId == sourceId);
        Assert(header.CVId == null, "standalone AAS JV allows a blank CV reference");
        Assert(await db.JournalVoucherDetails.AnyAsync(x => x.JournalVoucherHeaderId == header.JournalVoucherHeaderId && x.Debit == 25m),
            "standalone AAS JV detail links to the newly imported header");
        for (var column = 1; column <= sheet.Dimension.Columns; column++)
        {
            if (sheet.Cells[1, column].Text == "PostedBy") sheet.Cells[2, column].Value = "";
            if (sheet.Cells[1, column].Text == "PostedDate") sheet.Cells[2, column].Value = date;
        }
        var details = workbook.Workbook.Worksheets["JournalVoucherDetails"];
        details.Cells[2, 1, 2, 7].Copy(details.Cells[3, 1, 3, 7]);
        details.Cells[3, 4].Value = 0m; details.Cells[3, 5].Value = 25m; details.Cells[3, 7].Value = sourceId + 3;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var postedStream = new MemoryStream(await workbook.GetAsByteArrayAsync());
            var postedFile = new Microsoft.AspNetCore.Http.FormFile(postedStream, 0, postedStream.Length, "file", "posted.xlsx");
            controller.TempData.Clear();
            await (Task<Microsoft.AspNetCore.Mvc.IActionResult>)controllerType.GetMethod("AasImport")!.Invoke(controller, [postedFile, CancellationToken.None])!;
            Assert(!controller.TempData.ContainsKey("error") && !controller.TempData.ContainsKey("warning"), "posted AAS JV imports: " + controller.TempData["error"] + controller.TempData["warning"]);
            Assert(await db.JournalBooks.CountAsync(x => x.Reference == header.JournalVoucherHeaderNo) == 2, "AAS JV inserts journal book entries without duplicates");
            Assert(await db.GeneralLedgerBooks.CountAsync(x => x.Reference == header.JournalVoucherHeaderNo) == 2, "AAS JV inserts ledger entries without duplicates");
        }
    }
    Assert((await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM {db.Model.GetEntityTypes().Single(x => x.ClrType.Name == name).GetTableName()} WHERE original_document_id = {sourceId}").SingleAsync()) == 1,
        name + " AAS upload and reupload retain one document");
}
{
    var repo = scope.ServiceProvider.GetRequiredService<JournalVoucherRepo>();
    var row = new Accounting_System.Models.ViewModels.JournalVoucherUploadExcelFileViewModel { OriginalCVId = 901, OriginalSeriesNumber = "JV-LINK-CHECK" };
    var lookup = await repo.BuildLookupJournalVoucherHeaderContextForAasAsync([row], CancellationToken.None);
    Assert(repo.MapToJournalVoucherEntity(row, lookup).CVId == (await db.CheckVoucherHeaders.SingleAsync(x => x.OriginalDocumentId == 901)).CheckVoucherHeaderId,
        "AAS JV supplied CV source ID resolves to its local ID");
    row.OriginalCVId = int.MaxValue;
    lookup = await repo.BuildLookupJournalVoucherHeaderContextForAasAsync([row], CancellationToken.None);
    var rejected = false;
    try { repo.MapToJournalVoucherEntity(row, lookup); }
    catch (InvalidOperationException ex) when (ex.Message.Contains("check voucher first")) { rejected = true; }
    Assert(rejected, "AAS JV rejects an explicitly supplied missing CV reference");
}
{
    var payer = await db.CheckVoucherHeaders.FirstAsync(x => x.OriginalDocumentId == 901);
    var invoice = await db.CheckVoucherHeaders.FirstAsync(x => x.OriginalDocumentId != 901);
    using var workbook = new ExcelPackage();
    var sheet = Sheet(workbook, "MultipleCheckVoucherPayments");
    Set(sheet, (2, payer.OriginalDocumentId), (3, invoice.OriginalDocumentId), (4, 12.34m));
    var importer = ActivatorUtilities.CreateInstance<WorkbookImport>(scope.ServiceProvider);
    var method = typeof(WorkbookImport).GetMethod("ImportMultiplePaymentsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    for (var attempt = 0; attempt < 2; attempt++)
        await (Task)method.Invoke(importer, [sheet, CancellationToken.None])!;
    Assert(await db.MultipleCheckVoucherPayments.CountAsync(x => x.CheckVoucherHeaderPaymentId == payer.CheckVoucherHeaderId) == 1,
        "existing payment voucher outside workbook headers is accepted without duplicate allocations on reupload");
}
{
    using var workbook = new ExcelPackage();
    var header = CV(workbook);
    var payments = Sheet(workbook, "MultipleCheckVoucherPayments");
    var vouchers = await db.CheckVoucherHeaders.Include(x => x.Details).Take(2).ToListAsync();
    header.Cells[2, 31].Value = vouchers[0].CheckVoucherHeaderId;
    Set(payments, (2, vouchers[0].CheckVoucherHeaderId), (3, vouchers[1].CheckVoucherHeaderId), (4, 12.34m));
    var helper = typeof(WorkbookImport).Assembly.GetType("Accounting_System.Repository.VoucherExportDependencies")!;
    await (Task)helper.GetMethod("IncludeAsync")!.Invoke(null, [db, workbook, CancellationToken.None])!;
    Assert(Enumerable.Range(2, header.Dimension.Rows - 1).Any(row => header.Cells[row, 31].GetValue<int>() == vouchers[1].CheckVoucherHeaderId),
        "voucher export includes invoice header referenced only by a multiple payment");
    var rows = header.Dimension.Rows;
    await (Task)helper.GetMethod("IncludeAsync")!.Invoke(null, [db, workbook, CancellationToken.None])!;
    Assert(header.Dimension.Rows == rows, "voucher export dependency completion does not duplicate headers");
}
foreach (var (type, id, sheetName, column, label) in new[] {
    (typeof(Accounting_System.Controllers.SalesInvoiceController), (await db.SalesInvoices.FirstAsync()).SalesInvoiceId, "SalesInvoice", 5, "Customer Name"),
    (typeof(Accounting_System.Controllers.ReceivingReportController), (await db.ReceivingReports.FirstAsync()).ReceivingReportId, "ReceivingReport", 10, "Supplier Name") })
{
    var controller = (Microsoft.AspNetCore.Mvc.Controller)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
    var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user, RequestServices = scope.ServiceProvider };
    controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http };
    controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new TestTempData());
    var result = await (Task<Microsoft.AspNetCore.Mvc.IActionResult>)type.GetMethod("Export")!.Invoke(controller, [id.ToString(), CancellationToken.None])!;
    Assert(result is Microsoft.AspNetCore.Mvc.FileContentResult, sheetName + " enriched export succeeds");
    using var stream = new MemoryStream(((Microsoft.AspNetCore.Mvc.FileContentResult)result).FileContents);
    using var workbook = new ExcelPackage(stream);
    var sheet = workbook.Workbook.Worksheets[sheetName];
    Assert(sheet.Cells[1, column].Text == "Net of Vat" && sheet.Cells[1, column + 1].Text == "Vat", sheetName + " export contains tax columns");
    Assert(Enumerable.Range(1, sheet.Dimension.Columns).Any(x => sheet.Cells[1, x].Text == label), sheetName + " export contains party name");
    Assert(sheet.Cells[2, column].GetValue<decimal>() >= 0m, sheetName + " exports numeric net amount");
}
Console.WriteLine("All detection and AAS upload regression checks passed.");

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
