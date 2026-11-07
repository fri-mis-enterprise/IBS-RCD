using System.Globalization;
using System.Security.Claims;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.MasterFile;
using Accounting_System.Models.Reports;
using Accounting_System.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;

namespace Accounting_System.Repository;

// One transaction and one document workflow, whether a sheet is imported directly or as a dependency.
public partial class WorkbookImport(
    ApplicationDbContext _dbContext,
    GeneralRepo _generalRepo,
    SalesInvoiceRepo _salesInvoiceRepo,
    ServiceInvoiceRepo _serviceInvoiceRepo,
    PurchaseOrderRepo _purchaseOrderRepo,
    ReceivingReportRepo _receivingReportRepo,
    ReceiptRepo _receiptRepo,
    CreditMemoRepo _creditMemoRepo,
    DebitMemoRepo _debitMemoRepo,
    CheckVoucherRepo _checkVoucherRepo,
    JournalVoucherRepo _journalVoucherRepo,
    CustomerRepo _customerRepo,
    SupplierRepo _supplierRepo,
    ServiceRepo _serviceRepo)
{
    private ClaimsPrincipal _user = new();
    private string _uploadedBy = string.Empty;
    private string _ipAddress = string.Empty;
    private string _databaseName = "IBS-RCD";

    public static async Task<IActionResult> RunAsync(Controller controller, IFormFile? file,
        string document, CancellationToken cancellationToken, bool aas = false)
    {
        var action = document == "CollectionReceipt" ? "CollectionIndex" : "Index";
        var view = document switch
        {
            "Customers" => "Customer",
            "Services" => "Service",
            "CheckVoucherHeader" => "CheckVoucher",
            "JournalVoucherHeader" => "JournalVoucher",
            _ => document
        };
        IActionResult Redirect() => controller.RedirectToAction(action, new { view });

        if (file == null || file.Length == 0)
        {
            controller.TempData["error"] = "Please select a non-empty Excel file.";
            return Redirect();
        }

        // Existing repository registrations supply the dependencies; no import setup in Program.cs.
        try
        {
            var services = controller.HttpContext.RequestServices;
            await using var target = aas ? AasWorkbookServices.CreateTarget(services.GetRequiredService<AasDbContext>()) : null;
            var importer = ActivatorUtilities.CreateInstance<WorkbookImport>(
                target == null ? services : new AasWorkbookServices(services, target));
            importer._databaseName = aas ? "AAS" : "IBS-RCD";
            controller.TempData.Remove("importChanges");
            if (target != null) await AasWorkbookServices.PublishAsync(target, services.GetRequiredService<ApplicationDbContext>(), cancellationToken);
            await using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            importer._user = controller.User;
            importer._ipAddress = controller.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var hasChanges = await importer.ImportAsync(package, document, cancellationToken);
            if (target != null) await AasWorkbookServices.PublishAsync(target, services.GetRequiredService<ApplicationDbContext>(), cancellationToken);
            if (hasChanges)
                controller.TempData["importChanges"] = "";
            controller.TempData["success"] = "Uploading Success!";
        }
        catch (OperationCanceledException)
        {
            controller.TempData["error"] = "Import canceled. No changes were committed.";
        }
        catch (InvalidOperationException ex)
        {
            controller.TempData["warning"] = ex.Message;
        }
        catch (Exception ex)
        {
            controller.TempData["error"] = ex.Message;
        }
        return Redirect();
    }

    public async Task<bool> ImportAsync(ExcelPackage package, string document, CancellationToken cancellationToken,
        ClaimsPrincipal? user = null, string ipAddress = "")
    {
        if (user != null) _user = user;
        if (ipAddress.Length > 0) _ipAddress = ipAddress;
        var names = document switch
        {
            "CollectionReceipt" or "CreditMemo" or "DebitMemo" => new[] { "SalesInvoice", "ServiceInvoice", document },
            "ReceivingReport" => ["PurchaseOrder", document],
            "CheckVoucherHeader" => ["BankAccount", "PurchaseOrder", "ReceivingReport", document, "CheckVoucherTradePayments", "MultipleCheckVoucherPayments", "CheckVoucherDetails"],
            "JournalVoucherHeader" => ["BankAccount", "PurchaseOrder", "ReceivingReport", "CheckVoucherHeader", "CheckVoucherTradePayments", "MultipleCheckVoucherPayments", "CheckVoucherDetails", document, "JournalVoucherDetails"],
            _ => [document]
        };
        var sheets = package.Workbook.Worksheets.Where(x => names.Contains(x.Name))
            .ToDictionary(x => x.Name);
        if (!sheets.ContainsKey(document))
            throw new InvalidOperationException($"The Excel file must contain the '{document}' worksheet.");
        if (names.Length == 1 && package.Workbook.Worksheets.First().Name != document)
            throw new InvalidOperationException($"The first worksheet must be '{document}'.");
        foreach (var header in new[] { "CheckVoucherHeader", "JournalVoucherHeader" })
        {
            if (sheets.ContainsKey(header) && !sheets.ContainsKey(header.Replace("Header", "Details")))
                throw new InvalidOperationException($"The Excel file must contain details for '{header}'.");
        }
        if (sheets.Values.Any(x => x.Dimension == null))
            throw new InvalidOperationException("An import worksheet is empty. Include its column headers.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var pendingChangesBefore = await _dbContext.ImportExportLogs.CountAsync(x => x.Action == string.Empty, cancellationToken);
            _uploadedBy = await _generalRepo.GetUserFullNameAsync(_user.Identity?.Name ?? string.Empty) ?? string.Empty;
            // Reverse dependents before replacing the invoices, purchase orders or details they reference.
            await CaptureComparisonSnapshotsAsync(sheets, cancellationToken);
            await DetectPaymentSheetsAsync(sheets, cancellationToken);
            await ReverseExistingAsync(sheets, cancellationToken);
            foreach (var name in names)
            {
                if (!sheets.TryGetValue(name, out var worksheet)) continue;
                switch (name)
                {
                    case "BankAccount": await ImportBankAccountAsync(worksheet, cancellationToken); break;
                    case "ChartOfAccount": await ImportChartOfAccountAsync(worksheet, cancellationToken); break;
                    case "Customers": await ImportCustomerAsync(worksheet, cancellationToken); break;
                    case "Supplier": await ImportSupplierAsync(worksheet, cancellationToken); break;
                    case "Product": await ImportProductAsync(worksheet, cancellationToken); break;
                    case "Services": await ImportServiceAsync(worksheet, cancellationToken); break;
                    case "PurchaseOrder": await ImportPurchaseOrderAsync(worksheet, cancellationToken); break;
                    case "SalesInvoice": await ImportSalesInvoiceAsync(worksheet, cancellationToken); break;
                    case "ServiceInvoice": await ImportServiceInvoiceAsync(worksheet, cancellationToken); break;
                    case "ReceivingReport": await ImportReceivingReportAsync(worksheet, cancellationToken); break;
                    case "CollectionReceipt": await ImportReceiptAsync(worksheet, cancellationToken); break;
                    case "CreditMemo": await ImportCreditMemoAsync(worksheet, cancellationToken); break;
                    case "DebitMemo": await ImportDebitMemoAsync(worksheet, cancellationToken); break;
                    case "CheckVoucherHeader": await ImportCheckVoucherAsync(worksheet, cancellationToken); break;
                    case "CheckVoucherTradePayments": await ImportTradePaymentsAsync(worksheet, cancellationToken); break;
                    case "MultipleCheckVoucherPayments": await ImportMultiplePaymentsAsync(worksheet, cancellationToken); break;
                    case "CheckVoucherDetails":
                        await ImportCheckVoucherDetailsAsync(worksheet, cancellationToken);
                        await PostCheckVouchersAsync(sheets["CheckVoucherHeader"], cancellationToken);
                        break;
                    case "JournalVoucherHeader": await ImportJournalVoucherAsync(worksheet, cancellationToken); break;
                    case "JournalVoucherDetails":
                        await ImportJournalVoucherDetailsAsync(worksheet, cancellationToken);
                        await PostJournalVouchersAsync(sheets["JournalVoucherHeader"], cancellationToken);
                        break;
                    default: throw new InvalidOperationException($"Unsupported import: {name}.");
                }
            }
            await ReconcilePurchaseOrdersAsync(false, cancellationToken);
            await ReconcilePaymentsAsync(cancellationToken);
            await RecalculateInventoryAsync(_inventoryProducts, true, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var hasChanges = await _dbContext.ImportExportLogs.CountAsync(x => x.Action == string.Empty, cancellationToken) > pendingChangesBefore;
            await transaction.CommitAsync(cancellationToken);
            return hasChanges;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    internal static void SetStatus(BaseEntity entity)
    {
        entity.IsCanceled = !string.IsNullOrWhiteSpace(entity.CanceledBy) || entity.CanceledDate.GetValueOrDefault() != default;
        entity.IsVoided = !string.IsNullOrWhiteSpace(entity.VoidedBy) || entity.VoidedDate.GetValueOrDefault() != default;
        entity.IsPosted = !entity.IsCanceled && !entity.IsVoided &&
            (!string.IsNullOrWhiteSpace(entity.PostedBy) || entity.PostedDate.GetValueOrDefault() != default);
    }

    private void ApplyValues<TEntity>(TEntity existing, TEntity mapped, params string[] preserve) where TEntity : class
    {
        var entry = _dbContext.Entry(existing);
        var values = _dbContext.Entry(mapped).CurrentValues;
        foreach (var property in entry.Metadata.GetProperties())
            if (!property.IsPrimaryKey() && !preserve.Contains(property.Name))
                entry.Property(property.Name).CurrentValue = values[property.Name];
    }

    private async Task ImportDocumentsAsync<TRow, TEntity>(ExcelWorksheet worksheet, IEnumerable<TRow> rows,
        Func<TRow, string> key, IReadOnlyDictionary<string, TEntity> existing,
        Func<TRow, TEntity> map,
        Func<TEntity, TRow, Dictionary<string, (string, string)>> detect,
        Func<int, Dictionary<string, (string, string)>, string?, string, string, Task> log,
        Func<TRow, string, IEnumerable<AuditTrail>> audits,
        Func<TRow, TEntity, Task>? post, CancellationToken cancellationToken,
        params string[] preserve) where TEntity : BaseEntity
    {
        var supplementalRows = TransactionColumns.IndexRows<TEntity>(worksheet);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = new List<(TRow Row, TEntity Entity)>();
        foreach (var row in rows)
        {
            var series = key(row);
            if (string.IsNullOrWhiteSpace(series)) throw new InvalidOperationException("An original series number is missing.");
            if (!seen.Add(series)) continue;
            var mapped = map(row);
            existing.TryGetValue(series, out var prior);
            if (supplementalRows.TryGetValue(series, out var worksheetRow))
                TransactionColumns.Import(mapped, prior, worksheet, worksheetRow);
            if (existing.TryGetValue(series, out var entity))
            {
                var comparison = _comparisonSnapshots.TryGetValue((typeof(TEntity), series), out var snapshot)
                    ? (TEntity)snapshot : entity;
                var changes = detect(comparison, row);
                foreach (var field in TransactionColumns.Properties(typeof(TEntity)))
                {
                    var oldValue = System.Text.Json.JsonSerializer.Serialize(field.GetValue(comparison));
                    var newValue = System.Text.Json.JsonSerializer.Serialize(field.GetValue(mapped));
                    if (oldValue != newValue) changes[field.Name] = (oldValue, newValue);
                }
                if (changes.Count > 0) await log(entity.OriginalDocumentId, changes, _uploadedBy, series, _databaseName);
                ApplyValues(entity, mapped, preserve.Concat([nameof(BaseEntity.IsPrinted)]).ToArray());
            }
            else
            {
                entity = mapped;
                _dbContext.Set<TEntity>().Add(entity);
                _dbContext.AuditTrails.AddRange(audits(row, _ipAddress));
            }
            imported.Add((row, entity));
        }

        // Persist the complete worksheet's mapped Excel values before lifecycle processing.
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var (_, entity) in imported)
        {
            SetStatus(entity);
            if (entity is SalesInvoice sales)
            {
                _inventoryProducts.Add(sales.ProductId);
            }
            if (entity is ReceivingReport report)
            {
                if (report.POId is int orderId) _purchaseOrders.Add(orderId);
            }
            if (entity is SalesInvoice savedSales)
            {
                _paymentSalesInvoices.Add(savedSales.SalesInvoiceId);
                _uploadedSalesStatuses[savedSales.SalesInvoiceId] = savedSales.Status;
            }
            if (entity is ServiceInvoice savedService)
            {
                _paymentServiceInvoices.Add(savedService.ServiceInvoiceId);
                _uploadedServiceStatuses[savedService.ServiceInvoiceId] = savedService.Status;
            }
            if (entity is ReceivingReport savedReport) _paymentReports.Add(savedReport.ReceivingReportId);
            if (entity is CollectionReceipt savedReceipt) await TrackReceiptInvoicesAsync(savedReceipt, cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var (row, entity) in imported)
        {
            if (entity.IsPosted && post != null) await post(row, entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
