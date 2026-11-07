using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using OfficeOpenXml;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Accounting_System.Utility
{
    internal static class TransactionColumns
    {
        private static readonly NullabilityInfoContext Nullability = new();
        private static readonly Dictionary<Type, string[]> Fields = new()
        {
            [typeof(SalesInvoice)] = ["CustomerAddress", "CustomerTin", "PaymentStatus", "CwtBalance", "CwVatBalance", "CwtAmountPaid", "CwVatAmountPaid", "PurchaseOrderId", "ReceivingReportId", "DeliveryReceiptId", "CustomerOrderSlipId", "Terms", "DebitAmount", "CreditAmount"],
            [typeof(ServiceInvoice)] = ["CustomerName", "CustomerAddress", "CustomerTin", "CustomerBusinessStyle", "ServiceName", "ServicePercent", "PaymentStatus", "CwtBalance", "CwVatBalance", "CwtAmountPaid", "CwVatAmountPaid", "DebitAmount", "CreditAmount", "VatType", "HasEwt", "HasWvat", "DeliveryReceiptId"],
            [typeof(CollectionReceipt)] = ["MultipleSVId", "MultipleSV", "BankId", "BankAccountName", "BankAccountNumber", "EwtPeriodFrom", "EwtPeriodTo", "EwtReference1", "EwtReference2", "CwVatPeriodFrom", "CwVatPeriodTo", "CwVatReference1", "CwVatReference2", "F2306FileName", "F2307FileName", "SVMultipleAmount", "Status", "DepositedDate", "ClearedDate", "BatchNumber"],
            [typeof(DebitMemo)] = ["CwtAmount", "CwVatAmount", "Status", "ApprovedBy", "ApprovedDate"],
            [typeof(CreditMemo)] = ["CwtAmount", "CwVatAmount", "Status", "ApprovedBy", "ApprovedDate"],
            [typeof(PurchaseOrder)] = ["SupplierName", "SupplierAddress", "SupplierTin", "ProductName", "SupplierSalesOrderNo", "Status", "IsSubPo", "SubPoSeries", "CustomerId", "OldPoNo", "TriggerDate", "UnTriggeredQuantity", "PickUpPointId", "VatType", "TaxType", "TypeOfPurchase"],
            [typeof(ReceivingReport)] = ["SupplierDrNo", "WithdrawalCertificate", "AuthorityToLoadNo", "DeliveryReceiptId", "Status", "IsCostUpdated", "OldRRNo", "CostBasedOnSoa", "TaxPercentage"],
            [typeof(CheckVoucherHeader)] = ["OldCvNo", "SupplierName", "BankAccountName", "BankAccountNumber", "Status", "IsDocumentedByOtherCompany", "DocumentedByCompanyName", "SupportingFileSavedFileName", "SupportingFileSavedUrl", "DcpDate", "DcrDate", "IsAdvances", "IsEmployeeAdvance", "Address", "Tin", "VatType", "TaxType", "TaxPercent", "IsPayroll", "LiquidationDate", "ApprovedBy", "ApprovedDate"],
            [typeof(CheckVoucherDetail)] = ["IsDisplayEntry", "SubAccountType", "SubAccountId", "SubAccountName"],
            [typeof(JournalVoucherHeader)] = ["Status", "JvType", "ApprovedBy", "ApprovedDate", "Payee", "AutoReverseNextMonth"],
            [typeof(JournalVoucherDetail)] = ["SubAccountType", "SubAccountId", "SubAccountName"]
        };

        public static IEnumerable<PropertyInfo> Properties(Type type)
            => Fields.TryGetValue(type, out var names) ? names.Select(n => type.GetProperty(n)!) : [];

        public static Dictionary<string, int> IndexRows<T>(ExcelWorksheet sheet)
        {
            var columns = new ImportWorksheet(sheet);
            var names = typeof(T) == typeof(SalesInvoice) ? new[] { "OriginalSINo", "OriginalSeriesNumber", "SalesInvoiceNo" }
                : typeof(T) == typeof(ServiceInvoice) ? ["OriginalSVNo", "OriginalSeriesNumber", "ServiceInvoiceNo"]
                : typeof(T) == typeof(CollectionReceipt) ? ["OriginalCRNo", "OriginalSeriesNumber", "CollectionReceiptNo"]
                : typeof(T) == typeof(PurchaseOrder) ? ["OriginalPONo", "OriginalSeriesNumber", "PurchaseOrderNo", "OriginalJVNo"]
                : typeof(T) == typeof(ReceivingReport) ? ["OriginalRRNo", "OriginalSeriesNumber", "ReceivingReportNo"]
                : typeof(T) == typeof(DebitMemo) ? ["OriginalDMNo", "OriginalSeriesNumber", "DebitMemoNo"]
                : typeof(T) == typeof(CreditMemo) ? ["OriginalCMNo", "OriginalSeriesNumber", "CreditMemoNo"]
                : typeof(T) == typeof(CheckVoucherHeader) ? ["OriginalCVNo", "OriginalSeriesNumber"]
                : new[] { "OriginalJVNo", "OriginalSeriesNumber", "JournalVoucherHeaderNo" };
            var header = names.FirstOrDefault(columns.HasColumn);
            var rows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (header == null) return rows;
            for (var row = 2; row <= sheet.Dimension.Rows; row++)
                if (!columns.IsEmpty(row)) rows.TryAdd(columns.Text(row, header), row);
            return rows;
        }

        public static void Export<T>(ExcelWorksheet sheet, IEnumerable<T> records)
        {
            var fields = Properties(typeof(T)).ToArray();
            var headers = Enumerable.Range(1, sheet.Dimension.Columns).ToDictionary(c => sheet.Cells[1, c].Text, c => c, StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                if (headers.ContainsKey(field.Name)) continue;
                var column = headers.Count + 1;
                headers[field.Name] = column;
                sheet.Cells[1, column].Value = field.Name;
            }
            var row = 2;
            foreach (var record in records)
            {
                foreach (var field in fields)
                {
                    var value = field.GetValue(record);
                    sheet.Cells[row, headers[field.Name]].Value = value switch
                    {
                        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                        Array array => JsonSerializer.Serialize(array),
                        Enum enumeration => enumeration.ToString(),
                        _ => value
                    };
                }
                row++;
            }
        }

        public static void Import(object target, object? previous, ExcelWorksheet sheet, int row)
        {
            var columns = new ImportWorksheet(sheet);
            // Older IBSWeb workbooks may still contain this removed classification.
            var legacyType = columns.HasColumn("Type") ? columns.Text(row, "Type") : "";
            if (!string.IsNullOrWhiteSpace(legacyType) && !legacyType.Equals("Documented", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Worksheet '{sheet.Name}', row {row}: IBS-RCD accepts documented records only.");
            foreach (var field in Properties(target.GetType()))
            {
                if (!columns.HasColumn(field.Name))
                {
                    if (previous != null) field.SetValue(target, field.GetValue(previous));
                    continue;
                }
                var text = columns.Text(row, field.Name);
                var type = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;
                object? value;
                try
                {
                    value = string.IsNullOrWhiteSpace(text)
                        ? (field.PropertyType.IsValueType && Nullable.GetUnderlyingType(field.PropertyType) == null ? Activator.CreateInstance(type) : null)
                        : type == typeof(string) ? text
                        : type == typeof(DateOnly) ? DateOnly.FromDateTime(columns.Date(row, field.Name))
                        : type == typeof(DateTime) ? columns.Date(row, field.Name)
                        : type.IsArray ? ParseArray(text, type)
                        : type.IsEnum ? Enum.Parse(type, text, true)
                        : Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                    if (type == typeof(string) && value == null && Nullability.Create(field).WriteState == NullabilityState.NotNull)
                        value = string.Empty;
                    Validator.ValidateProperty(value, new ValidationContext(target) { MemberName = field.Name });
                    field.SetValue(target, value);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or JsonException or ArgumentException or ValidationException)
                {
                    throw new InvalidOperationException($"Worksheet '{sheet.Name}', row {row}: invalid '{field.Name}'.", ex);
                }
            }
        }

        private static object? ParseArray(string text, Type type)
        {
            if (text.TrimStart().StartsWith('[')) return JsonSerializer.Deserialize(text, type);
            if (type == typeof(decimal[]) && System.Text.RegularExpressions.Regex.IsMatch(text, @"^[+-]?\d{1,3}(,\d{3})+\.\d+$"))
                return new[] { decimal.Parse(text, CultureInfo.InvariantCulture) };
            var values = text.Contains(", ", StringComparison.Ordinal)
                ? text.Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (type == typeof(string[])) return values;
            if (type == typeof(int[])) return values.Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            if (type == typeof(decimal[])) return values.Select(x => decimal.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            throw new InvalidOperationException($"Unsupported transaction array type {type.Name}.");
        }
    }
}
