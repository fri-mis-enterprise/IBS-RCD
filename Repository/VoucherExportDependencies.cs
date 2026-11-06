using Accounting_System.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository;

internal static class VoucherExportDependencies
{
    // Payment sheets export source database keys. Include both ends of every exported allocation.
    public static async Task IncludeAsync(ApplicationDbContext db, ExcelPackage workbook, CancellationToken token)
    {
        var payments = workbook.Workbook.Worksheets["MultipleCheckVoucherPayments"];
        var headers = workbook.Workbook.Worksheets["CheckVoucherHeader"];
        var details = workbook.Workbook.Worksheets["CheckVoucherDetails"];
        if (payments?.Dimension == null || headers?.Dimension == null || details?.Dimension == null) return;
        int Column(ExcelWorksheet sheet, string name) => Enumerable.Range(1, sheet.Dimension.Columns)
            .Single(x => sheet.Cells[1, x].Text.Equals(name, StringComparison.OrdinalIgnoreCase));
        var payer = Column(payments, "CheckVoucherHeaderPaymentId");
        var invoice = Column(payments, "CheckVoucherHeaderInvoiceId");
        var original = Column(headers, "OriginalDocumentId");
        var wanted = Enumerable.Range(2, Math.Max(0, payments.Dimension.Rows - 1))
            .SelectMany(row => new[] { payments.Cells[row, payer].GetValue<int>(), payments.Cells[row, invoice].GetValue<int>() })
            .Where(x => x != 0).Distinct().ToList();
        var present = Enumerable.Range(2, Math.Max(0, headers.Dimension.Rows - 1))
            .Select(row => headers.Cells[row, original].GetValue<int>()).ToHashSet();
        var missing = wanted.Where(x => !present.Contains(x)).ToList();
        var vouchers = await db.CheckVoucherHeaders.Include(x => x.Details)
            .Where(x => missing.Contains(x.CheckVoucherHeaderId)).ToListAsync(token);
        if (vouchers.Count != missing.Count)
            throw new InvalidOperationException("An exported payment references a voucher missing from the source database.");
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["TransactionDate"] = "Date", ["ReceivingReportNo"] = "RRNo", ["SalesInvoiceNo"] = "SINo",
            ["PurchaseOrderNo"] = "PONo", ["OriginalBankId"] = "BankId", ["OriginalCVNo"] = "CheckVoucherHeaderNo",
            ["OriginalSupplierId"] = "SupplierId", ["OriginalDocumentId"] = "CheckVoucherHeaderId", ["CVHeaderId"] = "CheckVoucherHeaderId"
        };
        void Write(ExcelWorksheet sheet, object entity, bool detail)
        {
            var row = sheet.Dimension.Rows + 1;
            for (var column = 1; column <= sheet.Dimension.Columns; column++)
            {
                var heading = sheet.Cells[1, column].Text;
                var property = detail && heading == "OriginalDocumentId" ? "CheckVoucherDetailId" : aliases.GetValueOrDefault(heading, heading);
                var value = entity.GetType().GetProperty(property)?.GetValue(entity);
                sheet.Cells[row, column].Value = value switch {
                    DateOnly date => date.ToString("yyyy-MM-dd"),
                    DateTime date => date.ToString(Utility.CS.ImportDateTimeFormat),
                    decimal[] amounts => string.Join(" ", amounts.Select(x => x.ToString("N4"))),
                    string[] values => string.Join(", ", values), _ => value
                };
            }
        }
        foreach (var voucher in vouchers)
        {
            Write(headers, voucher, false);
            foreach (var detail in voucher.Details) Write(details, detail, true);
        }
    }
}
