using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.MasterFile;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Repository;

// Shared IBS-RCD/AAS proposal handling. Journal and inventory business actions remain separate.
internal static class IbsImportLogActions
{
    private static readonly Dictionary<string, Type> Tables = new()
    {
        ["SalesInvoice"] = typeof(SalesInvoice), ["ServiceInvoice"] = typeof(ServiceInvoice),
        ["CollectionReceipt"] = typeof(CollectionReceipt), ["CreditMemo"] = typeof(CreditMemo),
        ["DebitMemo"] = typeof(DebitMemo), ["PurchaseOrder"] = typeof(PurchaseOrder),
        ["ReceivingReport"] = typeof(ReceivingReport), ["CheckVoucherHeader"] = typeof(CheckVoucherHeader),
        ["JournalVoucherHeader"] = typeof(JournalVoucherHeader), ["CheckVoucherDetails"] = typeof(CheckVoucherDetail),
        ["JournalVoucherDetails"] = typeof(JournalVoucherDetail), ["BankAccount"] = typeof(BankAccount),
        ["Customer"] = typeof(Customer), ["Customers"] = typeof(Customer), ["Supplier"] = typeof(Supplier),
        ["Product"] = typeof(Product), ["Service"] = typeof(Services), ["Services"] = typeof(Services),
        ["ChartOfAccount"] = typeof(ChartOfAccount)
    };

    private static string? PaymentPrefix(string column) => column.StartsWith("CheckVoucherTradePayments.", StringComparison.Ordinal)
        ? "CheckVoucherTradePayments." : column.StartsWith("MultipleCheckVoucherPayments.", StringComparison.Ordinal)
            ? "MultipleCheckVoucherPayments." : null;

    public static async Task<List<ImportExportLog>> ReviewGroupAsync(DbContext db, ImportExportLog log,
        bool modify, CancellationToken token)
    {
        var prefix = PaymentPrefix(log.ColumnName);
        if (prefix == null) return [log];
        if (log.TableName != "CheckVoucherHeader") throw new InvalidOperationException("Invalid payment log target.");
        var related = db.Set<ImportExportLog>().Where(x => x.DatabaseName == log.DatabaseName && x.TableName == log.TableName &&
            x.DocumentRecordId == log.DocumentRecordId && x.DocumentNo == log.DocumentNo && x.ColumnName.StartsWith(prefix));
        if (modify && await related.AnyAsync(x => x.TimeStamp > log.TimeStamp, token))
            throw new InvalidOperationException("A newer payment proposal exists. Review the newest proposal.");
        var group = await related.Where(x => x.TimeStamp == log.TimeStamp).ToListAsync(token);
        if (group.Count == 0) throw new InvalidOperationException("The payment proposal was not found.");
        if (group.Any(x => x.Action != string.Empty || x.Executed))
            throw new InvalidOperationException("This payment change group has already been reviewed.");
        return group;
    }

    public static async Task ApplyAsync(DbContext db, ImportExportLog log, List<ImportExportLog> group, CancellationToken token)
    {
        if (PaymentPrefix(log.ColumnName) is { } prefix)
        {
            await ApplyPaymentsAsync(db, log, group, prefix, token);
            return;
        }
        var entity = await FindTargetAsync(db, log, token)
            ?? throw new InvalidOperationException("The record referenced by this log no longer exists.");
        var column = log.ColumnName switch
        {
            "SINo" when entity is SalesInvoice => nameof(SalesInvoice.SalesInvoiceNo),
            "SVNo" when entity is ServiceInvoice => nameof(ServiceInvoice.ServiceInvoiceNo),
            "CRNo" when entity is CollectionReceipt => nameof(CollectionReceipt.CollectionReceiptNo),
            "CMNo" when entity is CreditMemo => nameof(CreditMemo.CreditMemoNo),
            "DMNo" when entity is DebitMemo => nameof(DebitMemo.DebitMemoNo),
            "PONo" when entity is PurchaseOrder => nameof(PurchaseOrder.PurchaseOrderNo),
            "RRNo" when entity is ReceivingReport => nameof(ReceivingReport.ReceivingReportNo),
            "CVNo" when entity is CheckVoucherHeader => nameof(CheckVoucherHeader.CheckVoucherHeaderNo),
            "JVNo" when entity is JournalVoucherHeader => nameof(JournalVoucherHeader.JournalVoucherHeaderNo),
            "CVHeaderId" when entity is CheckVoucherDetail => nameof(CheckVoucherDetail.CheckVoucherHeaderId),
            "JVHeaderId" when entity is JournalVoucherDetail => nameof(JournalVoucherDetail.JournalVoucherHeaderId),
            _ => log.ColumnName
        };
        if (entity is CollectionReceipt receipt &&
            (column.StartsWith("SingleSalesInvoiceAmount(", StringComparison.Ordinal) || column.StartsWith("MultipleSalesInvoiceAmount(", StringComparison.Ordinal)))
        {
            ApplyReceiptAmountCorrection(receipt, column, log.AdjustedValue);
            return;
        }
        if (column is "AmountPaid" or "Balance" or "PaidDate" or "IsPaid" or "IsTaxAndVatPaid" or
            "IsPosted" or "IsCanceled" or "IsVoided" or "IsPrinted" ||
            entity is PurchaseOrder && column is "QuantityReceived" or "IsReceived" or "ReceivedDate" ||
            entity is Customer && column == "Number" || entity is Services && column is "ServiceNo" or "CurrentAndPreviousId" or "UnearnedId")
            throw new InvalidOperationException("This is a calculated or locally managed field. Keep the log; use the normal transaction actions to change it.");
        var entry = db.Entry(entity);
        var property = entry.Metadata.FindProperty(column);
        if (property == null || property.IsPrimaryKey()) throw new InvalidOperationException($"Unsupported import column '{column}'.");
        var value = ParseValue(log.AdjustedValue, property.ClrType);
        if (value is string text && property.GetMaxLength() is int max && text.Length > max)
            throw new InvalidOperationException($"'{column}' exceeds its maximum length of {max} characters.");
        entry.Property(column).CurrentValue = await ResolveReferenceAsync(db, entity, column, value, token);
        if (entity is BaseEntity document && column == "Status")
        {
            var status = ((string)value!).Trim();
            document.IsPosted = status.Equals("Posted", StringComparison.OrdinalIgnoreCase);
            document.IsVoided = status.Equals("Voided", StringComparison.OrdinalIgnoreCase);
            document.IsCanceled = status.Equals("Canceled", StringComparison.OrdinalIgnoreCase) ||
                                 status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<object?> FindTargetAsync(DbContext db, ImportExportLog log, CancellationToken token)
    {
        if (log.TableName == "Offsetting")
        {
            var receipt = await SourceAsync<CollectionReceipt>(db, "OriginalDocumentId", log.DocumentRecordId, token);
            return receipt == null ? null : await db.Set<Offsetting>().SingleOrDefaultAsync(x => x.Reference == receipt.OriginalSeriesNumber, token);
        }
        if (!Tables.TryGetValue(log.TableName, out var type)) throw new InvalidOperationException("Unsupported import table.");
        return type.Name switch
        {
            nameof(SalesInvoice) => await SourceAsync<SalesInvoice>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(ServiceInvoice) => await SourceAsync<ServiceInvoice>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(CollectionReceipt) => await SourceAsync<CollectionReceipt>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(CreditMemo) => await SourceAsync<CreditMemo>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(DebitMemo) => await SourceAsync<DebitMemo>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(PurchaseOrder) => await SourceAsync<PurchaseOrder>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(ReceivingReport) => await SourceAsync<ReceivingReport>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(CheckVoucherHeader) => await SourceAsync<CheckVoucherHeader>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(JournalVoucherHeader) => await SourceAsync<JournalVoucherHeader>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(CheckVoucherDetail) => await SourceAsync<CheckVoucherDetail>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            nameof(JournalVoucherDetail) => await SourceAsync<JournalVoucherDetail>(db, "OriginalDocumentId", log.DocumentRecordId, token),
            _ => await db.FindAsync(type, [log.DocumentRecordId], token)
        };
    }

    private static Task<TEntity?> SourceAsync<TEntity>(DbContext db, string property, int id, CancellationToken token) where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity));
        var member = Expression.Property(parameter, property);
        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(member, Expression.Convert(Expression.Constant(id), member.Type)), parameter);
        return db.Set<TEntity>().SingleOrDefaultAsync(predicate, token);
    }

    private static async Task<int?> LocalIdAsync<TEntity>(DbContext db, string sourceProperty, int sourceId,
        bool required, CancellationToken token) where TEntity : class
    {
        if (sourceId == 0 && !required) return null;
        var entity = sourceId == 0 ? null : await SourceAsync<TEntity>(db, sourceProperty, sourceId, token);
        if (entity == null) throw new InvalidOperationException($"Referenced {typeof(TEntity).Name} source ID {sourceId} was not found.");
        var entry = db.Entry(entity);
        return Convert.ToInt32(entry.Property(entry.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue);
    }

    private static async Task<object?> ResolveReferenceAsync(DbContext db, object entity, string column, object? value, CancellationToken token)
    {
        var entry = db.Entry(entity);
        var id = value is int number ? number : 0;
        (Type Type, string Source, string Local, bool Required)? reference = column switch
        {
            "OriginalCustomerId" => (typeof(Customer), "OriginalCustomerId", "CustomerId", true),
            "OriginalProductId" => (typeof(Product), "OriginalProductId", "ProductId", true),
            "OriginalSupplierId" => (typeof(Supplier), "OriginalSupplierId", "SupplierId", entity is PurchaseOrder),
            "OriginalBankId" => (typeof(BankAccount), "OriginalBankId", "BankId", false),
            "OriginalServicesId" => (typeof(Services), "OriginalServiceId", "ServicesId", true),
            "OriginalPOId" => (typeof(PurchaseOrder), "OriginalDocumentId", "POId", true),
            "OriginalSalesInvoiceId" => (typeof(SalesInvoice), "OriginalDocumentId", "SalesInvoiceId", false),
            "OriginalServiceInvoiceId" => (typeof(ServiceInvoice), "OriginalDocumentId", "ServiceInvoiceId", false),
            "OriginalCVId" => (typeof(CheckVoucherHeader), "OriginalDocumentId", "CVId", false),
            _ => null
        };
        if (entity is BaseEntity && reference is { } r && entry.Metadata.FindProperty(r.Local) != null)
        {
            object? referenced = null;
            if (id != 0)
            {
                referenced = r.Type.Name switch
                {
                    nameof(Customer) => await SourceAsync<Customer>(db, r.Source, id, token),
                    nameof(Product) => await SourceAsync<Product>(db, r.Source, id, token),
                    nameof(Supplier) => await SourceAsync<Supplier>(db, r.Source, id, token),
                    nameof(BankAccount) => await SourceAsync<BankAccount>(db, r.Source, id, token),
                    nameof(Services) => await SourceAsync<Services>(db, r.Source, id, token),
                    nameof(PurchaseOrder) => await SourceAsync<PurchaseOrder>(db, r.Source, id, token),
                    nameof(SalesInvoice) => await SourceAsync<SalesInvoice>(db, r.Source, id, token),
                    nameof(ServiceInvoice) => await SourceAsync<ServiceInvoice>(db, r.Source, id, token),
                    _ => await SourceAsync<CheckVoucherHeader>(db, r.Source, id, token)
                };
            }
            if (referenced == null && (r.Required || id != 0)) throw new InvalidOperationException($"Referenced {r.Type.Name} source ID {id} was not found.");
            entry.Property(r.Local).CurrentValue = referenced == null ? null : db.Entry(referenced).Property(db.Entry(referenced).Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue;
            if (entity is ReceivingReport report) report.PONo = (referenced as PurchaseOrder)?.PurchaseOrderNo;
            if (entity is CollectionReceipt receipt)
            {
                if (column == "OriginalSalesInvoiceId") receipt.SINo = (referenced as SalesInvoice)?.SalesInvoiceNo;
                if (column == "OriginalServiceInvoiceId") receipt.SVNo = (referenced as ServiceInvoice)?.ServiceInvoiceNo;
            }
            return value;
        }
        if (entity is CollectionReceipt && column == "MultipleSIId")
        {
            var sourceIds = (int[]?)value ?? [];
            var local = new List<int>();
            foreach (var sourceId in sourceIds) local.Add((await LocalIdAsync<SalesInvoice>(db, "OriginalDocumentId", sourceId, true, token))!.Value);
            return local.ToArray();
        }
        if (entity is CheckVoucherDetail cv)
        {
            if (column == "SupplierId") return await LocalIdAsync<Supplier>(db, "OriginalSupplierId", id, false, token);
            if (column == "CheckVoucherHeaderId" || column == "TransactionNo")
            {
                var header = column == "TransactionNo"
                    ? await db.Set<CheckVoucherHeader>().SingleOrDefaultAsync(x => x.OriginalSeriesNumber == (string?)value, token)
                    : await SourceAsync<CheckVoucherHeader>(db, "OriginalDocumentId", id, token);
                if (header == null) throw new InvalidOperationException("Referenced check voucher was not found.");
                cv.CheckVoucherHeaderId = header.CheckVoucherHeaderId;
                cv.TransactionNo = header.CheckVoucherHeaderNo!;
                return column == "TransactionNo" ? cv.TransactionNo : cv.CheckVoucherHeaderId;
            }
        }
        if (entity is JournalVoucherDetail jv && column is "JournalVoucherHeaderId" or "TransactionNo")
        {
            var header = column == "TransactionNo"
                ? await db.Set<JournalVoucherHeader>().SingleOrDefaultAsync(x => x.OriginalSeriesNumber == (string?)value, token)
                : await SourceAsync<JournalVoucherHeader>(db, "OriginalDocumentId", id, token);
            if (header == null) throw new InvalidOperationException("Referenced journal voucher was not found.");
            jv.JournalVoucherHeaderId = header.JournalVoucherHeaderId;
            jv.TransactionNo = header.JournalVoucherHeaderNo!;
            return column == "TransactionNo" ? jv.TransactionNo : jv.JournalVoucherHeaderId;
        }
        if (entity is CheckVoucherHeader voucher && column == "Reference" && !string.IsNullOrEmpty((string?)value))
        {
            if (voucher.CvType != "Payment") throw new InvalidOperationException("Only payment vouchers can have a voucher reference.");
            var referenced = await db.Set<CheckVoucherHeader>().SingleOrDefaultAsync(x => x.OriginalSeriesNumber == (string?)value, token)
                ?? throw new InvalidOperationException("Referenced voucher was not found.");
            return referenced.CheckVoucherHeaderNo;
        }
        if (db is AasDbContext && entity is ChartOfAccount && column == "ParentAccountId" && id != 0)
            value = await LocalIdAsync<ChartOfAccount>(db, "OriginalChartOfAccountId", id, true, token);
        if (entity is ChartOfAccount && column == "ParentAccountId" && value is 0) return null;
        if (entity is ChartOfAccount account && column == "ParentAccountId" && value != null)
        {
            var visited = new HashSet<int> { account.AccountId };
            var next = (int?)value;
            while (next is int parentId)
            {
                if (!visited.Add(parentId)) throw new InvalidOperationException("Parent account change would create a cycle.");
                var parent = await db.Set<ChartOfAccount>().FindAsync([parentId], token)
                    ?? throw new InvalidOperationException("Parent account was not found.");
                next = parent.ParentAccountId;
            }
        }
        return value;
    }

    private static object? ParseValue(string? text, Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (type == typeof(string)) return text ?? string.Empty;
        if (nullable != null && (string.IsNullOrWhiteSpace(text) || text == "null")) return null;
        type = nullable ?? type;
        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            if (string.IsNullOrWhiteSpace(text)) return Array.CreateInstance(element, 0);
            if (element == typeof(decimal)) return DecimalArray(text);
            var parts = text.Split(',', StringSplitOptions.TrimEntries);
            var result = Array.CreateInstance(element, parts.Length);
            for (var i = 0; i < parts.Length; i++) result.SetValue(ParseValue(parts[i], element), i);
            return result;
        }
        if (type == typeof(decimal)) return decimal.Parse(text!, NumberStyles.Number | NumberStyles.AllowParentheses, CultureInfo.CurrentCulture);
        if (type == typeof(int)) return int.Parse(text!, NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return bool.Parse(text!);
        if (type == typeof(DateOnly))
        {
            var date = DateOnly.ParseExact(text!, CS.DateOnly_Format_For_Validation, CultureInfo.InvariantCulture);
            return nullable != null && date == default ? null : date;
        }
        if (type == typeof(DateTime))
        {
            var date = DateTime.ParseExact(text!, [CS.ImportDateTimeFormat, "yyyy-MM-dd HH:mm:ss.ffffff", CS.DateTime_Format_For_Validation, "yyyy-MM-dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None);
            return nullable != null && date == default ? null : date;
        }
        throw new InvalidOperationException($"Unsupported import value type '{type.Name}'.");
    }

    private static decimal[] DecimalArray(string text)
    {
        var format = CultureInfo.CurrentCulture.NumberFormat;
        var group = Regex.Escape(format.NumberGroupSeparator);
        var point = Regex.Escape(format.NumberDecimalSeparator);
        var matches = Regex.Matches(text, $@"\(?[-+]?(?:\d{{1,3}}(?:{group}\d{{3}})+|\d+){point}\d{{4}}\)?");
        if (matches.Count == 0) return text.Split(',', StringSplitOptions.TrimEntries).Select(x => (decimal)ParseValue(x, typeof(decimal))!).ToArray();
        var end = 0;
        var values = new List<decimal>();
        foreach (Match match in matches)
        {
            var separator = text[end..match.Index].Trim();
            if ((values.Count == 0 && separator.Length != 0) || (values.Count > 0 && separator != ","))
                throw new FormatException("Invalid decimal array.");
            values.Add((decimal)ParseValue(match.Value, typeof(decimal))!);
            end = match.Index + match.Length;
        }
        if (text[end..].Trim().Length > 0) throw new FormatException("Invalid decimal array.");
        return values.ToArray();
    }

    private static void ApplyReceiptAmountCorrection(CollectionReceipt receipt, string column, string? text)
    {
        var amount = (decimal)ParseValue(text, typeof(decimal))!;
        if (column.StartsWith("MultipleSalesInvoiceAmount(", StringComparison.Ordinal))
        {
            var start = column.IndexOf('(') + 1;
            var end = column.LastIndexOf(')');
            if (end < start || receipt.MultipleSI == null || receipt.SIMultipleAmount == null) throw new InvalidOperationException("Invoice allocation was not found.");
            var index = Array.FindIndex(receipt.MultipleSI, x => string.Equals(x, column[start..end], StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= receipt.SIMultipleAmount.Length) throw new InvalidOperationException("Invoice allocation was not found.");
            receipt.Total += amount - receipt.SIMultipleAmount[index];
            receipt.SIMultipleAmount[index] = amount;
        }
        else receipt.Total = amount;
        var net = receipt.Total - receipt.EWT - receipt.WVAT;
        if (receipt.CashAmount != 0) receipt.CashAmount = net;
        if (receipt.CheckAmount != 0) receipt.CheckAmount = net;
        if (receipt.ManagerCheckAmount != 0) receipt.ManagerCheckAmount = net;
    }

    private static async Task ApplyPaymentsAsync(DbContext db, ImportExportLog log, List<ImportExportLog> group, string prefix, CancellationToken token)
    {
        var header = await SourceAsync<CheckVoucherHeader>(db, "OriginalDocumentId", log.DocumentRecordId, token)
            ?? throw new InvalidOperationException("Payment voucher was not found.");
        var changes = group.ToDictionary(x => x.ColumnName[prefix.Length..], x => x.AdjustedValue);
        var reports = new HashSet<int>();
        var invoices = new HashSet<int>();
        if (prefix == "CheckVoucherTradePayments.")
        {
            var existing = await db.Set<CVTradePayment>().Where(x => x.CheckVoucherId == header.CheckVoucherHeaderId).ToListAsync(token);
            var reportIds = existing.Select(x => x.DocumentId).ToList();
            var sourceIds = await db.Set<ReceivingReport>().Where(x => reportIds.Contains(x.ReceivingReportId)).ToDictionaryAsync(x => x.ReceivingReportId, x => x.OriginalDocumentId, token);
            var ordered = existing.OrderBy(x => sourceIds.GetValueOrDefault(x.DocumentId, x.DocumentId)).ThenBy(x => x.DocumentType).ThenBy(x => x.AmountPaid).ToList();
            var ids = Vector(changes, "DocumentId", ordered.Select(x => sourceIds.GetValueOrDefault(x.DocumentId, x.DocumentId)).ToArray());
            var types = Vector(changes, "DocumentType", ordered.Select(x => x.DocumentType).ToArray());
            var headers = Vector(changes, "CheckVoucherId", ordered.Select(_ => header.OriginalDocumentId).ToArray());
            var amounts = Vector(changes, "AmountPaid", ordered.Select(x => x.AmountPaid).ToArray());
            if (changes.Keys.Except(["DocumentId", "DocumentType", "CheckVoucherId", "AmountPaid"]).Any() ||
                ids.Length != types.Length || ids.Length != headers.Length || ids.Length != amounts.Length)
                throw new InvalidOperationException("The payment proposal is incomplete. Reimport the workbook to create a complete proposal.");
            var replacement = new List<CVTradePayment>();
            for (var i = 0; i < ids.Length; i++)
            {
                if (headers[i] != header.OriginalDocumentId || types[i] != "RR") throw new InvalidOperationException("Invalid payment voucher or document type.");
                var reportId = (await LocalIdAsync<ReceivingReport>(db, "OriginalDocumentId", ids[i], true, token))!.Value;
                reports.Add(reportId);
                replacement.Add(new CVTradePayment { CheckVoucherId = header.CheckVoucherHeaderId, DocumentId = reportId, DocumentType = "RR", AmountPaid = amounts[i] });
            }
            reports.UnionWith(existing.Select(x => x.DocumentId));
            db.Set<CVTradePayment>().RemoveRange(existing);
            db.Set<CVTradePayment>().AddRange(replacement);
        }
        else
        {
            var existing = await db.Set<MultipleCheckVoucherPayment>().Where(x => x.CheckVoucherHeaderPaymentId == header.CheckVoucherHeaderId).ToListAsync(token);
            var invoiceIds = existing.Select(x => x.CheckVoucherHeaderInvoiceId).ToList();
            var sourceIds = await db.Set<CheckVoucherHeader>().Where(x => invoiceIds.Contains(x.CheckVoucherHeaderId)).ToDictionaryAsync(x => x.CheckVoucherHeaderId, x => x.OriginalDocumentId, token);
            var ordered = existing.OrderBy(x => sourceIds.GetValueOrDefault(x.CheckVoucherHeaderInvoiceId, x.CheckVoucherHeaderInvoiceId)).ThenBy(x => x.AmountPaid).ToList();
            var payers = Vector(changes, "CheckVoucherHeaderPaymentId", ordered.Select(_ => header.OriginalDocumentId).ToArray());
            var ids = Vector(changes, "CheckVoucherHeaderInvoiceId", ordered.Select(x => sourceIds.GetValueOrDefault(x.CheckVoucherHeaderInvoiceId, x.CheckVoucherHeaderInvoiceId)).ToArray());
            var amounts = Vector(changes, "AmountPaid", ordered.Select(x => x.AmountPaid).ToArray());
            if (changes.Keys.Except(["CheckVoucherHeaderPaymentId", "CheckVoucherHeaderInvoiceId", "AmountPaid"]).Any() || payers.Length != ids.Length || ids.Length != amounts.Length)
                throw new InvalidOperationException("The payment proposal is incomplete. Reimport the workbook to create a complete proposal.");
            var replacement = new List<MultipleCheckVoucherPayment>();
            for (var i = 0; i < ids.Length; i++)
            {
                if (payers[i] != header.OriginalDocumentId) throw new InvalidOperationException("Invalid payment voucher.");
                var invoiceId = (await LocalIdAsync<CheckVoucherHeader>(db, "OriginalDocumentId", ids[i], true, token))!.Value;
                if (invoiceId == header.CheckVoucherHeaderId) throw new InvalidOperationException("A voucher cannot pay itself.");
                invoices.Add(invoiceId);
                replacement.Add(new MultipleCheckVoucherPayment { Id = Guid.NewGuid(), CheckVoucherHeaderPaymentId = header.CheckVoucherHeaderId, CheckVoucherHeaderInvoiceId = invoiceId, AmountPaid = amounts[i] });
            }
            invoices.UnionWith(existing.Select(x => x.CheckVoucherHeaderInvoiceId));
            db.Set<MultipleCheckVoucherPayment>().RemoveRange(existing);
            db.Set<MultipleCheckVoucherPayment>().AddRange(replacement);
        }
        await db.SaveChangesAsync(token);
        foreach (var id in reports)
        {
            var report = await db.Set<ReceivingReport>().FindAsync([id], token);
            if (report == null) continue;
            var payments = await db.Set<CVTradePayment>().Include(x => x.CV).Where(x => x.DocumentId == id && x.DocumentType == "RR" && !x.CV.IsVoided && !x.CV.IsCanceled).ToListAsync(token);
            report.AmountPaid = payments.Sum(x => x.AmountPaid);
            report.IsPaid = payments.Any(x => x.CV.IsPosted);
            report.PaidDate = payments.Where(x => x.CV.IsPosted).Select(x => x.CV.PostedDate ?? x.CV.CreatedDate).DefaultIfEmpty(default).Max();
        }
        foreach (var id in invoices)
        {
            var invoice = await db.Set<CheckVoucherHeader>().FindAsync([id], token);
            if (invoice == null) continue;
            var payments = await db.Set<MultipleCheckVoucherPayment>().Include(x => x.CheckVoucherHeaderPayment)
                .Where(x => x.CheckVoucherHeaderInvoiceId == id && x.CheckVoucherHeaderPayment!.IsPosted && !x.CheckVoucherHeaderPayment.IsCanceled && !x.CheckVoucherHeaderPayment.IsVoided).ToListAsync(token);
            invoice.AmountPaid = payments.Sum(x => x.AmountPaid);
            invoice.IsPaid = payments.Count > 0 && invoice.AmountPaid >= invoice.Total;
        }
    }

    private static T[] Vector<T>(Dictionary<string, string?> changes, string field, T[] current) =>
        changes.TryGetValue(field, out var text) ? (T[])ParseValue(text, typeof(T[]))! : current;
}
