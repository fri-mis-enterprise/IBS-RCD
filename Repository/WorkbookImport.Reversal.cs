using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository;

public partial class WorkbookImport
{
    private readonly HashSet<int?> _inventoryProducts = [];
    private readonly HashSet<int> _rebuiltReports = [];
    private readonly HashSet<int> _purchaseOrders = [];
    private readonly HashSet<int> _paymentSalesInvoices = [];
    private readonly Dictionary<int, string> _uploadedSalesStatuses = [];
    private readonly Dictionary<int, string> _uploadedServiceStatuses = [];
    private readonly HashSet<int> _paymentServiceInvoices = [];
    private readonly HashSet<int> _paymentReports = [];

    private async Task<List<TEntity>> ExistingAsync<TEntity>(IEnumerable<string> series, CancellationToken token)
        where TEntity : BaseEntity
    {
        var keys = series.Distinct().ToList();
        return await _dbContext.Set<TEntity>().Where(x => keys.Contains(x.OriginalSeriesNumber!)).ToListAsync(token);
    }

    private async Task ReverseExistingAsync(Dictionary<string, ExcelWorksheet> sheets, CancellationToken token)
    {
        if (sheets.TryGetValue("CollectionReceipt", out var cr))
            foreach (var entity in await ExistingAsync<CollectionReceipt>(_receiptRepo.ParseWorksheet(cr).Select(x => x.OriginalSeriesNumber), token))
            {
                await TrackReceiptInvoicesAsync(entity, token);
                _dbContext.CashReceiptBooks.RemoveRange(await _dbContext.CashReceiptBooks.Where(x => x.RefNo == entity.CollectionReceiptNo).ToListAsync(token));
                await RemoveLedgerAsync(entity.CollectionReceiptNo, token);
            }
        if (sheets.TryGetValue("JournalVoucherHeader", out var jv))
            foreach (var entity in await ExistingAsync<JournalVoucherHeader>(_journalVoucherRepo.ParseWorksheet(jv).Select(x => x.OriginalSeriesNumber), token))
            {
                _dbContext.JournalBooks.RemoveRange(await _dbContext.JournalBooks.Where(x => x.Reference == entity.JournalVoucherHeaderNo).ToListAsync(token));
                await RemoveLedgerAsync(entity.JournalVoucherHeaderNo, token);
            }
        if (sheets.TryGetValue("CheckVoucherHeader", out var cv))
            foreach (var entity in await ExistingAsync<CheckVoucherHeader>((await _checkVoucherRepo.ParseWorksheet(cv, token)).Select(x => x.OriginalSeriesNumber), token))
            {
                var payments = await _dbContext.CVTradePayments.Where(x => x.CheckVoucherId == entity.CheckVoucherHeaderId).ToListAsync(token);
                if (!entity.IsVoided && !entity.IsCanceled)
                    foreach (var payment in payments) await AdjustTradePaymentAsync(payment, -1, token);
                if (sheets.ContainsKey("CheckVoucherTradePayments"))
                {
                    _dbContext.CVTradePayments.RemoveRange(payments);
                }
                _dbContext.DisbursementBooks.RemoveRange(await _dbContext.DisbursementBooks.Where(x => x.CVNo == entity.CheckVoucherHeaderNo).ToListAsync(token));
                await RemoveLedgerAsync(entity.CheckVoucherHeaderNo, token);
            }
        if (sheets.TryGetValue("CreditMemo", out var cm))
            foreach (var entity in await ExistingAsync<CreditMemo>(_creditMemoRepo.ParseWorksheet(cm).Select(x => x.OriginalSeriesNumber), token))
                await RemoveSalesBooksAsync(entity.CreditMemoNo, token);
        if (sheets.TryGetValue("DebitMemo", out var dm))
            foreach (var entity in await ExistingAsync<DebitMemo>(_debitMemoRepo.ParseWorksheet(dm).Select(x => x.OriginalSeriesNumber), token))
                await RemoveSalesBooksAsync(entity.DebitMemoNo, token);
        if (sheets.TryGetValue("SalesInvoice", out var si))
            foreach (var entity in await ExistingAsync<SalesInvoice>(_salesInvoiceRepo.ParseWorksheet(si).Select(x => x.OriginalSeriesNumber), token))
            {
                await RemoveSalesBooksAsync(entity.SalesInvoiceNo, token);
                await RemoveInventoryAsync(entity.SalesInvoiceNo, token);
            }
        if (sheets.TryGetValue("ServiceInvoice", out var sv))
            foreach (var entity in await ExistingAsync<ServiceInvoice>(_serviceInvoiceRepo.ParseWorksheet(sv).Select(x => x.OriginalSeriesNumber), token))
                await RemoveSalesBooksAsync(entity.ServiceInvoiceNo, token);
        if (sheets.TryGetValue("ReceivingReport", out var rr))
            foreach (var entity in await ExistingAsync<ReceivingReport>(_receivingReportRepo.ParseWorksheet(rr).Select(x => x.OriginalSeriesNumber), token))
            {
                _rebuiltReports.Add(entity.ReceivingReportId);
                if (entity.POId is int orderId) _purchaseOrders.Add(orderId);
                _dbContext.PurchaseJournalBooks.RemoveRange(await _dbContext.PurchaseJournalBooks.Where(x => x.DocumentNo == entity.ReceivingReportNo).ToListAsync(token));
                await RemoveLedgerAsync(entity.ReceivingReportNo, token);
                await RemoveInventoryAsync(entity.ReceivingReportNo, token);
            }
        await ReconcilePurchaseOrdersAsync(true, token);
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task RemoveLedgerAsync(string? reference, CancellationToken token)
    {
        _dbContext.GeneralLedgerBooks.RemoveRange(await _dbContext.GeneralLedgerBooks.Where(x => x.Reference == reference).ToListAsync(token));
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task RemoveSalesBooksAsync(string? reference, CancellationToken token)
    {
        _dbContext.SalesBooks.RemoveRange(await _dbContext.SalesBooks.Where(x => x.SerialNo == reference).ToListAsync(token));
        await RemoveLedgerAsync(reference, token);
    }

    private async Task RemoveInventoryAsync(string? reference, CancellationToken token)
    {
        var removed = await _dbContext.Inventories.Where(x => x.Reference == reference).ToListAsync(token);
        var productIds = removed.Select(x => x.ProductId).Distinct().ToList();
        _inventoryProducts.UnionWith(productIds);
        _dbContext.Inventories.RemoveRange(removed);
        await _dbContext.SaveChangesAsync(token);
        await RecalculateInventoryAsync(productIds, false, token);
    }

    private async Task RecalculateInventoryAsync(IEnumerable<int?> productIds, bool validate, CancellationToken token)
    {
        // Rebuild from the remaining movements, not the removed movement's closing balance.
        foreach (var productId in productIds)
        {
            var movements = await _dbContext.Inventories.Where(x => x.ProductId == productId)
                .OrderBy(x => x.Date).ThenBy(x => x.Id).ToListAsync(token);
            decimal quantity = 0, total = 0, average = 0;
            foreach (var movement in movements)
            {
                if (movement.Particular == "Sales")
                {
                    movement.Cost = average;
                    movement.Total = movement.Quantity * average;
                    quantity -= movement.Quantity;
                    total -= movement.Total;
                    var costEntries = await _dbContext.GeneralLedgerBooks.Where(x => x.Reference == movement.Reference &&
                        (x.AccountNo.StartsWith("50101") || x.AccountNo.StartsWith("10104"))).ToListAsync(token);
                    foreach (var entry in costEntries)
                    {
                        entry.Debit = entry.AccountNo.StartsWith("50101") ? movement.Total : 0;
                        entry.Credit = entry.AccountNo.StartsWith("10104") ? movement.Total : 0;
                    }
                }
                else if (movement.Particular == "Actual Inventory(Loss)")
                {
                    quantity -= movement.Quantity;
                    total -= movement.Total;
                }
                else
                {
                    quantity += movement.Quantity;
                    total += movement.Total;
                }
                if (quantity != 0) average = total / quantity;
                movement.InventoryBalance = quantity;
                movement.TotalBalance = total;
                movement.AverageCost = average;
                if (validate && quantity < 0)
                    throw new InvalidOperationException($"Rebuilding inventory would leave negative stock at {movement.Reference ?? movement.Particular}.");
            }
        }
        await _dbContext.SaveChangesAsync(token);
    }

    private async Task ReconcilePurchaseOrdersAsync(bool excludeRebuiltReports, CancellationToken token)
    {
        foreach (var id in _purchaseOrders)
        {
            var order = await _dbContext.PurchaseOrders.FindAsync([id], token);
            if (order == null) continue;
            var reports = await _dbContext.ReceivingReports.Where(x => x.POId == id && x.IsPosted && !x.IsCanceled && !x.IsVoided).ToListAsync(token);
            order.QuantityReceived = reports.Where(x => !excludeRebuiltReports || !_rebuiltReports.Contains(x.ReceivingReportId)).Sum(x => x.QuantityReceived);
            if (order.QuantityReceived > order.Quantity)
                throw new InvalidOperationException($"Received quantity exceeds purchase order {order.PurchaseOrderNo}.");
            order.IsReceived = order.QuantityReceived == order.Quantity;
            order.ReceivedDate = order.IsReceived
                ? reports.Select(x => x.ReceivedDate?.ToDateTime(TimeOnly.MinValue) ?? default).DefaultIfEmpty(default).Max()
                : DateTime.MaxValue;
        }
    }

    private async Task ResolveReceiptInvoicesAsync(CollectionReceipt receipt, CancellationToken token)
    {
        if (receipt.SalesInvoiceId is int siId)
        {
            if (!await _dbContext.SalesInvoices.AnyAsync(x => x.SalesInvoiceId == siId, token))
                throw new InvalidOperationException($"Sales invoice missing for receipt {receipt.CollectionReceiptNo}.");
        }
        else if (receipt.ServiceInvoiceId is int svId)
        {
            if (!await _dbContext.ServiceInvoices.AnyAsync(x => x.ServiceInvoiceId == svId, token))
                throw new InvalidOperationException($"Service invoice missing for receipt {receipt.CollectionReceiptNo}.");
        }
        else if (receipt.MultipleSI is { Length: > 0 } numbers && receipt.SIMultipleAmount is { } amounts && numbers.Length == amounts.Length)
        {
            var ids = new List<int>();
            for (var i = 0; i < numbers.Length; i++)
            {
                var number = numbers[i];
                var invoice = await _dbContext.SalesInvoices.FirstOrDefaultAsync(x => x.SalesInvoiceNo == number || x.OriginalSeriesNumber == number, token)
                    ?? throw new InvalidOperationException($"Sales invoice {number} missing for receipt {receipt.CollectionReceiptNo}.");
                ids.Add(invoice.SalesInvoiceId);
            }
            // Source IDs belong to the exporting database; retain local IDs for future reversal.
            receipt.MultipleSIId = ids.ToArray();
        }
        else throw new InvalidOperationException($"Invoice selection missing or inconsistent for receipt {receipt.CollectionReceiptNo}.");
    }

    private async Task TrackReceiptInvoicesAsync(CollectionReceipt receipt, CancellationToken token)
    {
        if (receipt.SalesInvoiceId is int siId) _paymentSalesInvoices.Add(siId);
        if (receipt.ServiceInvoiceId is int svId) _paymentServiceInvoices.Add(svId);
        if (receipt.MultipleSI is { Length: > 0 } numbers)
            _paymentSalesInvoices.UnionWith(await _dbContext.SalesInvoices
                .Where(x => numbers.Contains(x.SalesInvoiceNo!) || numbers.Contains(x.OriginalSeriesNumber!))
                .Select(x => x.SalesInvoiceId).ToListAsync(token));
    }

    private static void AdjustSalesPayment(SalesInvoice invoice, decimal amount)
    {
        invoice.AmountPaid += amount;
        invoice.Balance = invoice.Amount - invoice.Discount - invoice.AmountPaid;
        invoice.IsPaid = invoice.Balance <= 0;
        invoice.Status = invoice.Balance < 0 ? "OverPaid" : invoice.IsPaid ? "Paid" : "Pending";
    }

    private async Task AdjustTradePaymentAsync(CVTradePayment payment, int direction, CancellationToken token)
    {
        if (payment.DocumentType != "RR") return;
        _paymentReports.Add(payment.DocumentId);
        var report = await _dbContext.ReceivingReports.FindAsync([payment.DocumentId], token)
            ?? throw new InvalidOperationException($"Receiving report missing for check voucher payment {payment.CheckVoucherId}.");
        report.AmountPaid += direction * payment.AmountPaid;
        report.IsPaid = false;
    }

    private async Task ReconcilePaymentsAsync(CancellationToken token)
    {
        // Payment totals are derived from local source documents, including when legacy books are missing.
        if (_paymentSalesInvoices.Count > 0 || _paymentServiceInvoices.Count > 0)
        {
            var receipts = await _dbContext.CollectionReceipts.Where(x => x.IsPosted && !x.IsVoided && !x.IsCanceled).ToListAsync(token);
            var sources = receipts.Select(x => x.CollectionReceiptNo).ToList();
            var offsets = await _dbContext.Offsettings.Where(x => sources.Contains(x.Source)).ToListAsync(token);
            foreach (var id in _paymentSalesInvoices)
            {
                var invoice = await _dbContext.SalesInvoices.FindAsync([id], token);
                if (invoice == null) continue;
                decimal paid = 0;
                foreach (var receipt in receipts)
                {
                    var receiptOffsets = offsets.Where(x => x.Source == receipt.CollectionReceiptNo);
                    if (receipt.SalesInvoiceId == id) paid += receipt.Total + receiptOffsets.Sum(x => x.Amount);
                    else if (receipt.SalesInvoiceId == null && receipt.MultipleSI is { } numbers && receipt.SIMultipleAmount is { } amounts)
                    {
                        for (var i = 0; i < Math.Min(numbers.Length, amounts.Length); i++)
                            if (numbers[i] == invoice.SalesInvoiceNo || numbers[i] == invoice.OriginalSeriesNumber)
                                paid += amounts[i] + receiptOffsets.Where(x => x.Reference == numbers[i]).Sum(x => x.Amount);
                    }
                }
                invoice.AmountPaid = paid;
                AdjustSalesPayment(invoice, 0);
                if (_uploadedSalesStatuses.TryGetValue(id, out var status)) invoice.Status = status;
            }
            foreach (var id in _paymentServiceInvoices)
            {
                var invoice = await _dbContext.ServiceInvoices.FindAsync([id], token);
                if (invoice == null) continue;
                var matching = receipts.Where(x => x.SalesInvoiceId == null && x.ServiceInvoiceId == id);
                invoice.AmountPaid = matching.Sum(x => x.Total + offsets.Where(o => o.Source == x.CollectionReceiptNo).Sum(o => o.Amount));
                invoice.Balance = invoice.Total - invoice.Discount - invoice.AmountPaid;
                invoice.IsPaid = invoice.Balance <= 0;
                invoice.Status = _uploadedServiceStatuses.TryGetValue(id, out var status)
                    ? status : invoice.Balance < 0 ? "OverPaid" : invoice.IsPaid ? "Paid" : "Pending";
            }
        }
        foreach (var id in _paymentReports)
        {
            var report = await _dbContext.ReceivingReports.FindAsync([id], token);
            if (report == null) continue;
            var payments = await _dbContext.CVTradePayments.Include(x => x.CV)
                .Where(x => x.DocumentType == "RR" && x.DocumentId == id && !x.CV.IsVoided && !x.CV.IsCanceled).ToListAsync(token);
            report.AmountPaid = payments.Sum(x => x.AmountPaid);
            report.IsPaid = payments.Any(x => x.CV.IsPosted);
            report.PaidDate = payments.Where(x => x.CV.IsPosted)
                .Select(x => x.CV.PostedDate ?? x.CV.CreatedDate).DefaultIfEmpty(default).Max();
        }
    }
}
