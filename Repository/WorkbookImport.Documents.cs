using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.Reports;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository;

public partial class WorkbookImport
{
    private async Task ImportPurchaseOrderAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _purchaseOrderRepo.ParseWorksheet(worksheet);
        var lookup = await _purchaseOrderRepo.BuildLookupPurchaseOrderContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingPurchaseOrder,
            row => _purchaseOrderRepo.MapToPurchaseOrderEntity(row, lookup),
            (entity, row) => _purchaseOrderRepo.Detect(entity, row, lookup.ExistingLogs),
            _purchaseOrderRepo.LogChangesAsync, _purchaseOrderRepo.AuditTrails,
            null, cancellationToken, "QuantityReceived", "IsReceived", "ReceivedDate");
    }

    private async Task ImportSalesInvoiceAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _salesInvoiceRepo.ParseWorksheet(worksheet);
        var lookup = await _salesInvoiceRepo.BuildLookupSalesInvoiceContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingInvoices,
            row => _salesInvoiceRepo.MapToSalesInvoiceEntity(row, lookup),
            (entity, row) => _salesInvoiceRepo.Detect(entity, row, lookup.ExistingLogs),
            _salesInvoiceRepo.LogChangesAsync, _salesInvoiceRepo.AuditTrails,
            async (row, entity) =>
            {
                await _salesInvoiceRepo.SalesBookPosting(row, cancellationToken);
                await _salesInvoiceRepo.GeneralLedgerBookPosting(row, cancellationToken);
                await _salesInvoiceRepo.AddSalesToInventoryBookAsync(row, cancellationToken);
            }, cancellationToken, "AmountPaid", "Balance", "IsPaid", "IsTaxAndVatPaid");
    }

    private async Task ImportServiceInvoiceAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _serviceInvoiceRepo.ParseWorksheet(worksheet);
        var lookup = await _serviceInvoiceRepo.BuildLookupServiceInvoiceContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingInvoices,
            row => _serviceInvoiceRepo.MapToServiceInvoiceEntity(row, lookup),
            (entity, row) => _serviceInvoiceRepo.Detect(entity, row, lookup.ExistingLogs),
            _serviceInvoiceRepo.LogChangesAsync, _serviceInvoiceRepo.AuditTrails,
            async (row, entity) =>
            {
                await _serviceInvoiceRepo.SalesBookPosting(row, cancellationToken);
                await _serviceInvoiceRepo.GeneralLedgerBookPosting(row, cancellationToken);
            }, cancellationToken, "AmountPaid", "Balance", "IsPaid");
    }

    private async Task ImportReceivingReportAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _receivingReportRepo.ParseWorksheet(worksheet);
        var lookup = await _receivingReportRepo.BuildLookupReceivingReportContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingReceivingReport,
            row => _receivingReportRepo.MapToReceivingReportEntity(row, lookup),
            (entity, row) => _receivingReportRepo.Detect(entity, row, lookup.ExistingLogs),
            _receivingReportRepo.LogChangesAsync, _receivingReportRepo.AuditTrails,
            async (row, entity) =>
            {
                if (row.ReceivedDate == default) throw new InvalidOperationException($"Received date missing for RR#{entity.ReceivingReportNo}.");
                entity.PurchaseOrder = await _receivingReportRepo.GetPurchaseOrderAsync(entity.POId!.Value, cancellationToken);
                _inventoryProducts.Add(entity.PurchaseOrder.ProductId);
                await _receivingReportRepo.PostAsync(entity, _user, cancellationToken);
            }, cancellationToken, "AmountPaid", "IsPaid", "PaidDate");
    }

    private async Task ImportReceiptAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _receiptRepo.ParseWorksheet(worksheet);
        var lookup = await _receiptRepo.BuildLookupCollectionReceiptContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingCollectionReceipt,
            row => _receiptRepo.MapToCollectionReceiptEntity(row, lookup),
            (entity, row) => _receiptRepo.Detect(entity, row, lookup.ExistingLogs),
            _receiptRepo.LogChangesAsync, _receiptRepo.AuditTrails,
            async (row, entity) =>
            {
                await _receiptRepo.CollectionReceiptPosting(row, cancellationToken);
                await ResolveReceiptInvoicesAsync(entity, cancellationToken);
            }, cancellationToken);
        foreach (var row in rows)
            await _receiptRepo.CheckSalesInvoiceAmountsAsync(row, lookup.ExistingLogs, cancellationToken);
        var keys = rows.Select(x => x.OriginalSeriesNumber).ToList();
        var terminal = await _dbContext.CollectionReceipts.Where(x => keys.Contains(x.OriginalSeriesNumber) && (x.IsVoided || x.IsCanceled)).Select(x => x.CollectionReceiptNo).ToListAsync(cancellationToken);
        _dbContext.Offsettings.RemoveRange(await _dbContext.Offsettings.Where(x => terminal.Contains(x.Source)).ToListAsync(cancellationToken));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportCreditMemoAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _creditMemoRepo.ParseWorksheet(worksheet);
        var lookup = await _creditMemoRepo.BuildLookupCreditMemoContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingCreditMemo,
            row => _creditMemoRepo.MapToCreditMemoEntity(row, lookup),
            (entity, row) => _creditMemoRepo.Detect(entity, row, lookup.ExistingLogs),
            _creditMemoRepo.LogChangesAsync, _creditMemoRepo.AuditTrails,
            async (row, entity) =>
            {
                await _creditMemoRepo.SalesBookPosting(row, cancellationToken);
                await _creditMemoRepo.GeneralLedgerBookPosting(row, cancellationToken);
            }, cancellationToken);
    }

    private async Task ImportDebitMemoAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _debitMemoRepo.ParseWorksheet(worksheet);
        var lookup = await _debitMemoRepo.BuildLookupDebitMemoContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingDebitMemo,
            row => _debitMemoRepo.MapToDebitMemoEntity(row, lookup),
            (entity, row) => _debitMemoRepo.Detect(entity, row, lookup.ExistingLogs),
            _debitMemoRepo.LogChangesAsync, _debitMemoRepo.AuditTrails,
            async (row, entity) =>
            {
                await _debitMemoRepo.SalesBookPosting(row, cancellationToken);
                await _debitMemoRepo.GeneralLedgerBookPosting(row, cancellationToken);
            }, cancellationToken);
    }

    private async Task ImportJournalVoucherAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = _journalVoucherRepo.ParseWorksheet(worksheet);
        var lookup = await _journalVoucherRepo.BuildLookupCheckVoucherHeaderContextAsync(rows, cancellationToken);
        await ImportDocumentsAsync(rows, row => row.OriginalSeriesNumber, lookup.ExistingJournalVoucherHeader,
            row => _journalVoucherRepo.MapToJournalVoucherEntity(row, lookup),
            (entity, row) => _journalVoucherRepo.Detect(entity, row, lookup.ExistingLogs),
            _journalVoucherRepo.LogChangesAsync, _journalVoucherRepo.AuditTrails,
            null, cancellationToken);
        var keys = rows.Select(x => x.OriginalSeriesNumber).ToList();
        _journalVoucherIds.AddRange(await _dbContext.JournalVoucherHeaders.Where(x => keys.Contains(x.OriginalSeriesNumber!))
            .Select(x => x.JournalVoucherHeaderId).ToListAsync(cancellationToken));
    }

}
