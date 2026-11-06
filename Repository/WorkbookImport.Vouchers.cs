using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository;

public partial class WorkbookImport
{
    private readonly List<int> _checkVoucherIds = [];
    private readonly List<int> _journalVoucherIds = [];

    private async Task ImportCheckVoucherAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var rows = await _checkVoucherRepo.ParseWorksheet(worksheet, token);
        var lookup = await _checkVoucherRepo.BuildLookupCheckVoucherHeaderContextAsync(rows, token);
        var mapped = new List<CheckVoucherHeader>();
        var lastNumber = await _dbContext.CheckVoucherHeaders.OrderByDescending(x => x.CheckVoucherHeaderNo)
            .Select(x => x.CheckVoucherHeaderNo).FirstOrDefaultAsync(token);
        await ImportDocumentsAsync(rows.OrderBy(x => x.Date).ThenBy(x => x.CreatedDate), row => row.OriginalSeriesNumber,
            lookup.ExistingCheckVoucherHeader,
            row =>
            {
                string? number;
                if (lookup.ExistingCheckVoucherHeader.TryGetValue(row.OriginalSeriesNumber, out var existing))
                    number = existing.CheckVoucherHeaderNo;
                else
                    number = lastNumber = _checkVoucherRepo.GenerateCodeForUploadingExcelFile(lastNumber, token);
                var entity = _checkVoucherRepo.MapToCheckVoucherHeaderEntity(row, mapped, lookup, number);
                if (row.CvType == "Payment" && string.IsNullOrEmpty(entity.Reference))
                    entity.Reference = lookup.ExistingCheckVoucherHeader.Values.FirstOrDefault(x => x.OriginalSeriesNumber == row.Reference)?.CheckVoucherHeaderNo;
                mapped.Add(entity);
                return entity;
            },
            (entity, row) => _checkVoucherRepo.Detect(entity, row, lookup.ExistingLogs),
            _checkVoucherRepo.LogChangesAsync, _checkVoucherRepo.AuditTrails, null, token, "CheckVoucherHeaderNo");
        var keys = rows.Select(x => x.OriginalSeriesNumber).ToList();
        _checkVoucherIds.AddRange(await _dbContext.CheckVoucherHeaders.Where(x => keys.Contains(x.OriginalSeriesNumber!))
            .Select(x => x.CheckVoucherHeaderId).ToListAsync(token));
    }

    private async Task ImportTradePaymentsAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var rows = _checkVoucherRepo.ParseWorksheetCvTradePayment(worksheet);
        var lookup = await _checkVoucherRepo.BuildLookupCvTradePaymentContextAsync(rows, token);
        foreach (var row in rows)
        {
            var payment = _checkVoucherRepo.MapToCvTradePaymentEntity(row, lookup);
            if (!_checkVoucherIds.Contains(payment.CheckVoucherId) || payment.DocumentId == 0)
                throw new InvalidOperationException("A trade payment references a missing voucher or receiving report.");
            _dbContext.CVTradePayments.Add(payment);
        }
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task ImportMultiplePaymentsAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var rows = _checkVoucherRepo.ParseWorksheetCvMultiplePayment(worksheet);
        var lookup = await _checkVoucherRepo.BuildLookupCvMultiplePaymentContextAsync(rows, token);
        var payerIds = _checkVoucherIds.Concat(lookup.CheckVoucherHeaderPaymentId.Values).Distinct().ToList();
        _dbContext.MultipleCheckVoucherPayments.RemoveRange(await _dbContext.MultipleCheckVoucherPayments
            .Where(x => payerIds.Contains(x.CheckVoucherHeaderPaymentId)).ToListAsync(token));
        foreach (var row in rows)
        {
            var payment = _checkVoucherRepo.MapToCvMultiplePaymentEntity(row, lookup);
            if (payment.CheckVoucherHeaderPaymentId == 0 || payment.CheckVoucherHeaderInvoiceId == 0)
                throw new InvalidOperationException($"MultipleCheckVoucherPayments: source payment voucher ID {row.CheckVoucherHeaderPaymentId}" +
                    (payment.CheckVoucherHeaderPaymentId == 0 ? " was not found" : " was found") +
                    $"; source invoice voucher ID {row.CheckVoucherHeaderInvoiceId}" +
                    (payment.CheckVoucherHeaderInvoiceId == 0 ? " was not found" : " was found") + ". Upload the missing voucher first or include it in CheckVoucherHeader.");
            _dbContext.MultipleCheckVoucherPayments.Add(payment);
        }
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task ImportCheckVoucherDetailsAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var rows = _checkVoucherRepo.ParseWorksheetCheckVoucherDetails(worksheet);
        var lookup = await _checkVoucherRepo.BuildLookupCheckVoucherDetailsContextAsync(rows, token);
        var seen = new HashSet<int>();
        var incomingIds = rows.Select(x => x.OriginalDocumentId).ToList();
        _dbContext.CheckVoucherDetails.RemoveRange(await _dbContext.CheckVoucherDetails
            .Where(x => _checkVoucherIds.Contains(x.CheckVoucherHeaderId!.Value) && !incomingIds.Contains(x.OriginalDocumentId ?? 0)).ToListAsync(token));
        foreach (var row in rows)
        {
            if (!seen.Add(row.OriginalDocumentId)) continue;
            var mapped = _checkVoucherRepo.MapToCheckVoucherDetailsEntity(row, lookup);
            if (mapped.CheckVoucherHeaderId == null || !_checkVoucherIds.Contains(mapped.CheckVoucherHeaderId.Value))
                throw new InvalidOperationException("A voucher detail references a missing header.");
            if (lookup.ExistingCheckVoucherDetail.TryGetValue(row.OriginalDocumentId, out var existing))
            {
                var changes = _checkVoucherRepo.DetectCvDetails(existing, row, lookup.ExistingLogs);
                if (changes.Count > 0) await _checkVoucherRepo.LogChangesForCVDAsync(existing.OriginalDocumentId, changes,
                    _uploadedBy, mapped.TransactionNo, _databaseName);
                ApplyValues(existing, mapped);
            }
            else _dbContext.CheckVoucherDetails.Add(mapped);
        }
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task PostCheckVouchersAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var headers = await _dbContext.CheckVoucherHeaders.Where(x => _checkVoucherIds.Contains(x.CheckVoucherHeaderId)).ToListAsync(token);
        foreach (var header in headers)
        {
            var payments = await _dbContext.CVTradePayments.Where(x => x.CheckVoucherId == header.CheckVoucherHeaderId).ToListAsync(token);
            if (!header.IsCanceled && !header.IsVoided)
                foreach (var payment in payments)
                {
                    await AdjustTradePaymentAsync(payment, 1, token);
                    if (header.IsPosted && payment.DocumentType == "RR")
                    {
                        var report = await _dbContext.ReceivingReports.FindAsync([payment.DocumentId], token);
                        report!.IsPaid = true;
                        report.PaidDate = DateTime.Now;
                    }
                }
            if (!header.IsPosted) continue;
            if (!await _dbContext.CheckVoucherDetails.AnyAsync(x => x.CheckVoucherHeaderId == header.CheckVoucherHeaderId, token))
                throw new InvalidOperationException($"Posted voucher {header.CheckVoucherHeaderNo} has no details.");
            await _checkVoucherRepo.GeneralLedgerBookPosting(header, token);
            await _checkVoucherRepo.DisbursementBookPosting(header, token);
        }
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task ImportJournalVoucherDetailsAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var headers = _journalVoucherIds;
        var rows = _journalVoucherRepo.ParseWorksheetJournalVoucherDetails(worksheet);
        var lookup = await _journalVoucherRepo.BuildLookupJournalVoucherDetailsContextAsync(rows, token);
        var seen = new HashSet<int>();
        var incomingIds = rows.Select(x => x.OriginalDocumentId).ToList();
        _dbContext.JournalVoucherDetails.RemoveRange(await _dbContext.JournalVoucherDetails
            .Where(x => headers.Contains(x.JournalVoucherHeaderId) && !incomingIds.Contains(x.OriginalDocumentId ?? 0)).ToListAsync(token));
        foreach (var row in rows)
        {
            if (!seen.Add(row.OriginalDocumentId)) continue;
            var mapped = _journalVoucherRepo.MapToJournalVoucherDetailsEntity(row, lookup);
            if (!headers.Contains(mapped.JournalVoucherHeaderId))
                throw new InvalidOperationException("A journal detail references a missing header.");
            if (lookup.ExistingJournalVoucherDetail.TryGetValue(row.OriginalDocumentId, out var existing))
            {
                var changes = _journalVoucherRepo.DetectJvDetails(existing, row, lookup.ExistingLogs);
                if (changes.Count > 0) await _journalVoucherRepo.LogChangesForJVDAsync(existing.OriginalDocumentId, changes,
                    _uploadedBy, mapped.TransactionNo, _databaseName);
                ApplyValues(existing, mapped);
            }
            else _dbContext.JournalVoucherDetails.Add(mapped);
        }
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task PostJournalVouchersAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        var headers = await _dbContext.JournalVoucherHeaders.Where(x => _journalVoucherIds.Contains(x.JournalVoucherHeaderId) && x.IsPosted).ToListAsync(token);
        foreach (var header in headers)
        {
            if (!await _dbContext.JournalVoucherDetails.AnyAsync(x => x.JournalVoucherHeaderId == header.JournalVoucherHeaderId, token))
                throw new InvalidOperationException($"Posted journal voucher {header.JournalVoucherHeaderNo} has no details.");
            await _journalVoucherRepo.GeneralLedgerBookPosting(header, token);
            await _journalVoucherRepo.JournalBookPosting(header, token);
        }
        await _dbContext.SaveChangesAsync(token);
    }
}
