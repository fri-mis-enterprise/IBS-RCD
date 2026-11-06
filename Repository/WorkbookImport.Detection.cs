using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository;

public partial class WorkbookImport
{
    private readonly Dictionary<(Type Type, string Series), BaseEntity> _comparisonSnapshots = [];

    // Reversal changes PO aggregates. Compare against the database as it was before this upload.
    private async Task CaptureComparisonSnapshotsAsync(Dictionary<string, ExcelWorksheet> sheets, CancellationToken token)
    {
        foreach (var (name, sheet) in sheets)
            switch (name)
            {
                case "SalesInvoice": await CaptureAsync<SalesInvoice>(sheet, "OriginalSINo", token); break;
                case "ServiceInvoice": await CaptureAsync<ServiceInvoice>(sheet, "OriginalSVNo", token); break;
                case "PurchaseOrder": await CaptureAsync<PurchaseOrder>(sheet, "OriginalPONo", token, "OriginalJVNo"); break;
                case "ReceivingReport": await CaptureAsync<ReceivingReport>(sheet, "OriginalRRNo", token); break;
                case "CollectionReceipt": await CaptureAsync<CollectionReceipt>(sheet, "OriginalCRNo", token); break;
                case "CreditMemo": await CaptureAsync<CreditMemo>(sheet, "OriginalCMNo", token); break;
                case "DebitMemo": await CaptureAsync<DebitMemo>(sheet, "OriginalDMNo", token); break;
                case "CheckVoucherHeader": await CaptureAsync<CheckVoucherHeader>(sheet, "OriginalCVNo", token); break;
                case "JournalVoucherHeader": await CaptureAsync<JournalVoucherHeader>(sheet, "OriginalJVNo", token); break;
            }
    }

    private async Task CaptureAsync<TEntity>(ExcelWorksheet worksheet, string seriesHeader, CancellationToken token,
        params string[] aliases) where TEntity : BaseEntity
    {
        var sheet = new ImportWorksheet(worksheet);
        var keys = Enumerable.Range(2, Math.Max(0, worksheet.Dimension.Rows - 1))
            .Where(row => !sheet.IsEmpty(row))
            .Select(row => sheet.Text(row, seriesHeader, aliases.Concat(["OriginalSeriesNumber", typeof(TEntity).Name + "No"]).ToArray()))
            .Distinct().ToList();
        foreach (var entity in await _dbContext.Set<TEntity>().Where(x => keys.Contains(x.OriginalSeriesNumber!)).ToListAsync(token))
            _comparisonSnapshots[(typeof(TEntity), entity.OriginalSeriesNumber!)] =
                (BaseEntity)_dbContext.Entry(entity).CurrentValues.ToObject();
    }
    private Task DetectPaymentSheetsAsync(Dictionary<string, ExcelWorksheet> sheets, CancellationToken token) =>
        _checkVoucherRepo.DetectPaymentWorksheetsAsync(sheets.GetValueOrDefault("CheckVoucherHeader"),
            sheets.GetValueOrDefault("CheckVoucherTradePayments"), sheets.GetValueOrDefault("MultipleCheckVoucherPayments"),
            _uploadedBy, false, token);
}
