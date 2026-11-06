using Accounting_System.Data;
using Accounting_System.DTOs;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.Reports;
using Accounting_System.Models.ViewModels;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;

namespace Accounting_System.Repository
{
    public class CheckVoucherRepo
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly AasDbContext _aasDbContext;

        private readonly GeneralRepo _generalRepo;

        public CheckVoucherRepo(ApplicationDbContext dbContext, AasDbContext aasDbContext, GeneralRepo generalRepo)
        {
            _dbContext = dbContext;
            _aasDbContext = aasDbContext;
            _generalRepo = generalRepo;
        }

        public async Task<List<CheckVoucherHeader>> GetCheckVouchersAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.CheckVoucherHeaders
                .Include(s => s.Supplier)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GenerateCVNo(CancellationToken cancellationToken = default)
        {
            var checkVoucher = await _dbContext
                .CheckVoucherHeaders
                .OrderByDescending(s => s.CheckVoucherHeaderNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (checkVoucher != null)
            {
                string lastSeries = checkVoucher.CheckVoucherHeaderNo ?? throw new InvalidOperationException("CVNo is null pls Contact MIS Enterprise");
                string numericPart = lastSeries.Substring(2);
                int incrementedNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0, 2) + incrementedNumber.ToString("D10");
            }

            return $"CV{1.ToString("D10")}";
        }

        public async Task<string> GenerateAasCvNo(CancellationToken cancellationToken = default)
        {
            var checkVoucher = await _aasDbContext
                .CheckVoucherHeaders
                .OrderByDescending(s => s.CheckVoucherHeaderNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (checkVoucher != null)
            {
                string lastSeries = checkVoucher.CheckVoucherHeaderNo ?? throw new InvalidOperationException("CVNo is null pls Contact MIS Enterprise");
                string numericPart = lastSeries.Substring(2);
                int incrementedNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0, 2) + incrementedNumber.ToString("D10");
            }

            return $"CV{1.ToString("D10")}";
        }

        public async Task UpdateInvoicingVoucher(decimal paymentAmount, int invoiceVoucherId, CancellationToken cancellationToken = default)
        {
            var invoiceVoucher = await _dbContext.CheckVoucherHeaders
                .FirstOrDefaultAsync(x => x.CheckVoucherHeaderId == invoiceVoucherId, cancellationToken);

            if (invoiceVoucher != null)
            {
                invoiceVoucher.AmountPaid += paymentAmount;

                if (invoiceVoucher.AmountPaid >= invoiceVoucher.Total)
                {
                    invoiceVoucher.IsPaid = true;
                }
            }
            else
            {
                throw new InvalidOperationException($"Check voucher with id '{invoiceVoucherId}' not found.");
            }
        }

        public async Task LogChangesAsync(int id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            var batch = DateTime.Now;
            foreach (var change in changes)
            {
                var logReport = new ImportExportLog
                {
                    Id = Guid.NewGuid(),
                    TableName = "CheckVoucherHeader",
                    DocumentRecordId = id,
                    ColumnName = change.Key,
                    Module = "Check Voucher Header",
                    OriginalValue = change.Value.OriginalValue,
                    AdjustedValue = change.Value.NewValue,
                    TimeStamp = batch,
                    UploadedBy = modifiedBy,
                    Action = string.Empty,
                    Executed = false,
                    DatabaseName = databaseName,
                    DocumentNo = seriesNumber
                };
                await _dbContext.AddAsync(logReport);
            }
        }

        public async Task LogChangesForCVDAsync(int? id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            foreach (var change in changes)
            {
                var logReport = new ImportExportLog()
                {
                    Id = Guid.NewGuid(),
                    TableName = "CheckVoucherDetails",
                    DocumentRecordId = id!.Value,
                    ColumnName = change.Key,
                    Module = "Check Voucher Details",
                    OriginalValue = change.Value.OriginalValue,
                    AdjustedValue = change.Value.NewValue,
                    TimeStamp = DateTime.Now,
                    UploadedBy = modifiedBy,
                    Action = string.Empty,
                    Executed = false,
                    DatabaseName = databaseName,
                    DocumentNo = seriesNumber
                };
                await _dbContext.AddAsync(logReport);
            }
        }

        public async Task<IReadOnlyList<CheckVoucherUploadExcelFileViewModel>> ParseWorksheet(
            ExcelWorksheet worksheet,
            CancellationToken cancellationToken = default)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<CheckVoucherUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;


            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;


                rows.Add(new CheckVoucherUploadExcelFileViewModel
                {
                    CheckVoucherHeaderNo = string.Empty,
                    Date = DateOnly.FromDateTime(sheet.Date(row, "TransactionDate", true, "Date")),
                    RRNo = sheet.Text(row, "ReceivingReportNo", "RRNo").Split(',').Select(rrNo => rrNo.Trim()).ToArray(),
                    SINo = sheet.Text(row, "SalesInvoiceNo", "SINo").Split(',').Select(rrNo => rrNo.Trim()).ToArray(),
                    PONo = sheet.Text(row, "PurchaseOrderNo", "PONo").Split(',').Select(rrNo => rrNo.Trim()).ToArray(),
                    Particulars = sheet.Text(row, "Particulars"),
                    CheckNo = sheet.Text(row, "CheckNo"),
                    Category = sheet.Text(row, "Category"),
                    Payee = sheet.Text(row, "Payee"),
                    CheckDate = DateOnly.FromDateTime(sheet.Date(row, "CheckDate")),
                    StartDate = DateOnly.FromDateTime(sheet.Date(row, "StartDate")),
                    EndDate = DateOnly.FromDateTime(sheet.Date(row, "EndDate")),
                    NumberOfMonths = sheet.Value<int>(row, "NumberOfMonths"),
                    NumberOfMonthsCreated = sheet.Value<int>(row, "NumberOfMonthsCreated"),
                    LastCreatedDate = sheet.Date(row, "LastCreatedDate"),
                    AmountPerMonth = sheet.Value<decimal>(row, "AmountPerMonth"),
                    IsComplete = sheet.Value<bool>(row, "IsComplete"),
                    AccruedType = sheet.Text(row, "AccruedType"),
                    Reference = sheet.Text(row, "Reference"),
                    CreatedBy = sheet.Text(row, "CreatedBy"),
                    CreatedDate = sheet.Date(row, "CreatedDate", true),
                    PostedBy = sheet.Text(row, "PostedBy"),
                    PostedDate = sheet.Date(row, "PostedDate"),
                    Total = sheet.Value<decimal>(row, "Total"),
                    Amount = sheet.Text(row, "Amount").Split(' ').Select(arrayAmount =>
                                                             decimal.TryParse(arrayAmount.Trim(), out decimal amount) ? amount : 0).ToArray(),
                    CheckAmount = sheet.Value<decimal>(row, "CheckAmount"),
                    CvType = sheet.Text(row, "CvType"),
                    AmountPaid = sheet.Value<decimal>(row, "AmountPaid"),
                    IsPaid = sheet.Value<bool>(row, "IsPaid"),
                    CancellationRemarks = sheet.Text(row, "CancellationRemarks"),
                    OriginalBankId = sheet.Value<int>(row, "OriginalBankId"),
                    OriginalSeriesNumber = sheet.Text(row, "OriginalCVNo", "OriginalSeriesNumber"),
                    OriginalSupplierId = sheet.Value<int>(row, "OriginalSupplierId"),
                    OriginalDocumentId = sheet.Value<int>(row, "OriginalDocumentId"),
                    CanceledBy = sheet.Text(row, "CanceledBy"),
                    CanceledDate = sheet.Date(row, "CanceledDate"),
                    VoidedBy = sheet.Text(row, "VoidedBy"),
                    VoidedDate = sheet.Date(row, "VoidedDate"),
                    EditedBy = sheet.Text(row, "EditedBy"),
                    EditedDate = sheet.Date(row, "EditedDate")
                });
            }

            return rows;
        }

        public async Task<FindCheckVoucherInDbContextDto> BuildLookupCheckVoucherHeaderContextAsync(
            IEnumerable<CheckVoucherUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalSupplierIds = rows.Select(r => r.OriginalSupplierId).Distinct().ToList();
            var originalBankIds = rows.Select(r => r.OriginalBankId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindCheckVoucherInDbContextDto
            {
                ExistingCheckVoucherHeader = await _dbContext.CheckVoucherHeaders
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber!))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber!, cancellationToken),

                SupplierId = await _dbContext.Suppliers
                    .Where(x => originalSupplierIds.Contains(x.OriginalSupplierId!.Value))
                    .GroupBy(x => x.OriginalSupplierId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSupplierId!.Value, x => x.SupplierId, cancellationToken),

                BankId = await _dbContext.BankAccounts
                    .Where(x => originalBankIds.Contains(x.OriginalBankId!.Value))
                    .GroupBy(x => x.OriginalBankId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalBankId!.Value, x => x.BankAccountId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public CheckVoucherHeader MapToCheckVoucherHeaderEntity(
            CheckVoucherUploadExcelFileViewModel row,
            List<CheckVoucherHeader> checkVoucherInvoices,
            FindCheckVoucherInDbContextDto context, string? checkVoucherHeaderNo)
        {
            var getReference = checkVoucherInvoices
                .Where(x => x.OriginalSeriesNumber == row.Reference)
                .Select(x => x.CheckVoucherHeaderNo)
                .FirstOrDefault();
            return new CheckVoucherHeader
            {
                CheckVoucherHeaderNo = checkVoucherHeaderNo,
                Date = row.Date,
                RRNo = row.RRNo,
                SINo = row.SINo,
                PONo = row.PONo,
                Particulars = row.Particulars,
                CheckNo = row.CheckNo,
                Category = row.Category,
                Payee = row.Payee,
                CheckDate = row.CheckDate,
                StartDate = row.StartDate,
                EndDate = row.EndDate,
                NumberOfMonths = row.NumberOfMonths,
                NumberOfMonthsCreated = row.NumberOfMonthsCreated,
                LastCreatedDate = row.LastCreatedDate,
                AmountPerMonth = row.AmountPerMonth,
                IsComplete = row.IsComplete,
                AccruedType = row.AccruedType,
                Reference = row.CvType == "Payment" ? getReference : string.Empty,
                CreatedBy = row.CreatedBy,
                CreatedDate = row.CreatedDate,
                IsPosted = !string.IsNullOrEmpty(row.PostedBy),
                PostedBy = row.PostedBy,
                PostedDate = row.PostedDate,
                Total = row.Total,
                Amount = row.Amount,
                CheckAmount = row.CheckAmount,
                CvType = row.CvType,
                AmountPaid = row.AmountPaid,
                IsPaid = row.IsPaid,
                CancellationRemarks = row.CancellationRemarks,
                IsCanceled = !string.IsNullOrEmpty(row.CanceledBy),
                CanceledBy = row.CanceledBy,
                CanceledDate = row.CanceledDate,
                IsVoided = !string.IsNullOrEmpty(row.VoidedBy),
                VoidedBy = row.VoidedBy,
                VoidedDate = row.VoidedDate,
                EditedBy = row.EditedBy,
                EditedDate = row.EditedDate,
                OriginalBankId = row.OriginalBankId,
                OriginalSeriesNumber = row.OriginalSeriesNumber,
                OriginalSupplierId = row.OriginalSupplierId,
                OriginalDocumentId = row.OriginalDocumentId,

                SupplierId = context.SupplierId.TryGetValue(row.OriginalSupplierId, out var supplierId)
                    ? supplierId
                    : null,

                BankId = row.CvType != "Invoicing" && row.OriginalBankId != 0 ? context.BankId.TryGetValue(row.OriginalBankId, out var bankId)
                    ? bankId
                    : throw new InvalidOperationException(
                        $"CheckVoucherHeader '{row.OriginalSeriesNumber}': bank source ID {row.OriginalBankId} was not found. Import the matching BankAccount master or include its BankAccount sheet in this workbook.")
                    : null
            };
        }

        public IEnumerable<AuditTrail> AuditTrails(
            CheckVoucherUploadExcelFileViewModel row,
            string machineName)
        {
            var audits = new List<AuditTrail>();

            if (!string.IsNullOrWhiteSpace(row.CreatedBy))
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CreatedBy,
                    Activity = $"Create new check vouchcer# {row.CheckVoucherHeaderNo}",
                    DocumentType = "Check Voucher",
                    MachineName = machineName,
                    Date = row.CreatedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.PostedBy) && row.PostedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.PostedBy,
                    Activity = $"Posted check voucher# {row.CheckVoucherHeaderNo}",
                    DocumentType = "Check Voucher",
                    MachineName = machineName,
                    Date = row.PostedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.CanceledBy) && row.CanceledDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CanceledBy,
                    Activity = $"Cancelled check voucher# {row.CheckVoucherHeaderNo}",
                    DocumentType = "Check Voucher",
                    MachineName = machineName,
                    Date = row.CanceledDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.VoidedBy) && row.VoidedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.VoidedBy,
                    Activity = $"Voided check voucher# {row.CheckVoucherHeaderNo}",
                    DocumentType = "Check Voucher",
                    MachineName = machineName,
                    Date = row.VoidedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.EditedBy) && row.EditedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.EditedBy,
                    Activity = $"Edited check voucher# {row.CheckVoucherHeaderNo}",
                    DocumentType = "Check Voucher",
                    MachineName = machineName,
                    Date = row.EditedDate
                });
            }

            return audits;
        }

        public Dictionary<string, (string Original, string New)> Detect(
            CheckVoucherHeader entity,
            CheckVoucherUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "Date",
                entity.Date.ToString(CS.DateOnly_Format_For_Validation),
                row.Date.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "RRNo",
                string.Join(",", entity.RRNo ?? Array.Empty<string>()),
                string.Join(",", row.RRNo));

            _generalRepo.Compare(changes, logs, "SINo",
                string.Join(",", entity.SINo ?? Array.Empty<string>()),
                string.Join(",", row.SINo));

            _generalRepo.Compare(changes, logs, "PONo",
                string.Join(",", entity.PONo ?? Array.Empty<string>()),
                string.Join(",", row.PONo));

            _generalRepo.Compare(changes, logs, "Particulars",
                StringHelper.NormalizeString(entity.Particulars),
                row.Particulars);

            _generalRepo.Compare(changes, logs, "CheckNo",
                StringHelper.NormalizeString(entity.CheckNo),
                row.CheckNo);

            _generalRepo.Compare(changes, logs, "Category",
                StringHelper.NormalizeString(entity.Category),
                row.Category);

            _generalRepo.Compare(changes, logs, "Payee",
                StringHelper.NormalizeString(entity.Payee),
                row.Payee);

            _generalRepo.Compare(changes, logs, "CheckDate",
                (entity.CheckDate ?? DateOnly.MinValue).ToString(CS.DateOnly_Format_For_Validation),
                row.CheckDate.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "StartDate",
                (entity.StartDate ?? DateOnly.MinValue).ToString(CS.DateOnly_Format_For_Validation),
                row.StartDate.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "EndDate",
                (entity.EndDate ?? DateOnly.MinValue).ToString(CS.DateOnly_Format_For_Validation),
                row.EndDate.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "NumberOfMonths",
                entity.NumberOfMonths.ToString(),
                row.NumberOfMonths.ToString());

            _generalRepo.Compare(changes, logs, "NumberOfMonthsCreated",
                entity.NumberOfMonthsCreated.ToString(),
                row.NumberOfMonthsCreated.ToString());

            _generalRepo.Compare(changes, logs, "LastCreatedDate",
                (entity.LastCreatedDate ?? DateTime.MinValue).ToString(CS.ImportDateTimeFormat),
                row.LastCreatedDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "AmountPerMonth",
                entity.AmountPerMonth.ToString(CS.Four_Decimal_Format),
                row.AmountPerMonth.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "IsComplete",
                entity.IsComplete.ToString(),
                row.IsComplete.ToString());

            _generalRepo.Compare(changes, logs, "AccruedType",
                StringHelper.NormalizeString(entity.AccruedType),
                row.AccruedType);

            _generalRepo.Compare(changes, logs, "CreatedBy",
                StringHelper.NormalizeString(entity.CreatedBy),
                row.CreatedBy);

            _generalRepo.Compare(changes, logs, "CreatedDate",
                entity.CreatedDate.ToString(CS.ImportDateTimeFormat),
                row.CreatedDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "PostedBy",
                StringHelper.NormalizeString(entity.PostedBy),
                row.PostedBy);

            _generalRepo.Compare(changes, logs, "PostedDate",
                (entity.PostedDate ?? DateTime.MinValue).ToString(CS.ImportDateTimeFormat),
                row.PostedDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "Total",
                entity.Total.ToString(CS.Four_Decimal_Format),
                row.Total.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Amount",
                string.Join(",", (entity.Amount ?? Array.Empty<decimal>()).Select(x => x.ToString(CS.Four_Decimal_Format))),
                string.Join(",", row.Amount.Select(x => x.ToString(CS.Four_Decimal_Format))));

            _generalRepo.Compare(changes, logs, "CheckAmount",
                entity.CheckAmount.ToString(CS.Four_Decimal_Format),
                row.CheckAmount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "CvType",
                StringHelper.NormalizeString(entity.CvType),
                row.CvType);

            _generalRepo.Compare(changes, logs, "CancellationRemarks",
                StringHelper.NormalizeString(entity.CancellationRemarks),
                row.CancellationRemarks);

            _generalRepo.Compare(changes, logs, "OriginalBankId",
                (entity.OriginalBankId ?? 0).ToString(),
                row.OriginalBankId.ToString());

            _generalRepo.Compare(changes, logs, "OriginalSeriesNumber",
                StringHelper.NormalizeString(entity.OriginalSeriesNumber),
                row.OriginalSeriesNumber);

            _generalRepo.Compare(changes, logs, "OriginalSupplierId",
                (entity.OriginalSupplierId ?? 0).ToString(),
                row.OriginalSupplierId.ToString());

            _generalRepo.Compare(changes, logs, "OriginalDocumentId",
                entity.OriginalDocumentId.ToString(),
                row.OriginalDocumentId.ToString());

            _generalRepo.Compare(changes, logs, "EditedBy",
                StringHelper.NormalizeString(entity.EditedBy), StringHelper.NormalizeString(row.EditedBy));
            _generalRepo.Compare(changes, logs, "EditedDate",
                (entity.EditedDate ?? default).ToString(CS.ImportDateTimeFormat),
                row.EditedDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "CanceledBy",
                StringHelper.NormalizeString(entity.CanceledBy), StringHelper.NormalizeString(row.CanceledBy));
            _generalRepo.Compare(changes, logs, "CanceledDate",
                (entity.CanceledDate ?? default).ToString(CS.ImportDateTimeFormat),
                row.CanceledDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "VoidedBy",
                StringHelper.NormalizeString(entity.VoidedBy), StringHelper.NormalizeString(row.VoidedBy));
            _generalRepo.Compare(changes, logs, "VoidedDate",
                (entity.VoidedDate ?? default).ToString(CS.ImportDateTimeFormat),
                row.VoidedDate.ToString(CS.ImportDateTimeFormat));

            DbContext referenceDb = _aasDbContext.ChangeTracker.Entries<CheckVoucherHeader>().Any(x => ReferenceEquals(x.Entity, entity))
                ? _aasDbContext : _dbContext;
            var sourceReference = referenceDb.Set<CheckVoucherHeader>().Where(x => x.CheckVoucherHeaderNo == entity.Reference)
                .Select(x => x.OriginalSeriesNumber).FirstOrDefault() ?? entity.Reference;
            _generalRepo.Compare(changes, logs, "Reference",
                StringHelper.NormalizeString(sourceReference), StringHelper.NormalizeString(row.Reference));

            return changes;
        }

        public string GenerateCodeForUploadingExcelFile(string? getLastCheckVoucherHeaderNo, CancellationToken cancellationToken = default)
        {
            if (!getLastCheckVoucherHeaderNo.IsNullOrEmpty())
            {
                var lastSeries = getLastCheckVoucherHeaderNo ?? throw new InvalidOperationException("CVNo is null pls Contact MIS Enterprise");
                var numericPart = lastSeries.Substring(2);
                var incrementedNumber = int.Parse(numericPart) + 1;
                return lastSeries.Substring(0, 2) + incrementedNumber.ToString("D10");
            }

            return $"CV{1:D10}";
        }

        public async Task DetectPaymentWorksheetsAsync(ExcelWorksheet? headerWorksheet, ExcelWorksheet? tradeWorksheet,
            ExcelWorksheet? multipleWorksheet, string uploadedBy, bool forAas, CancellationToken token)
        {
            if (headerWorksheet == null) return;
            DbContext sourceDb = forAas ? _aasDbContext : _dbContext;
            var databaseName = forAas ? "AAS" : "IBS-RCD";
            var keys = (await ParseWorksheet(headerWorksheet, token)).Select(x => x.OriginalSeriesNumber).ToList();
            var headers = await sourceDb.Set<CheckVoucherHeader>().Where(x => keys.Contains(x.OriginalSeriesNumber!)).ToListAsync(token);
            if (headers.Count == 0) return;
            var ids = headers.Select(x => x.CheckVoucherHeaderId).ToList();
            if (tradeWorksheet is { } tradeSheet)
            {
                var rows = ParseWorksheetCvTradePayment(tradeSheet);
                if (rows.Any(x => x.DocumentType != "RR")) throw new InvalidOperationException("Trade payments must reference DocumentType RR.");
                var existing = await sourceDb.Set<CVTradePayment>().Where(x => ids.Contains(x.CheckVoucherId)).ToListAsync(token);
                var reportIds = existing.Select(x => x.DocumentId).ToList();
                var reports = await sourceDb.Set<ReceivingReport>().Where(x => reportIds.Contains(x.ReceivingReportId))
                    .ToDictionaryAsync(x => x.ReceivingReportId, x => x.OriginalDocumentId, token);
                foreach (var header in headers)
                {
                    var before = existing.Where(x => x.CheckVoucherId == header.CheckVoucherHeaderId).Select(x => new CvTradePaymentUploadExcelFileViewModel
                    {
                        DocumentId = reports.GetValueOrDefault(x.DocumentId, x.DocumentId), DocumentType = x.DocumentType,
                        CheckVoucherId = header.OriginalDocumentId, AmountPaid = x.AmountPaid
                    });
                    var changes = DetectTradePayments(before, rows.Where(x => x.CheckVoucherId == header.OriginalDocumentId));
                    if (changes.Count > 0) await LogChangesAsync(header.OriginalDocumentId, changes, uploadedBy, header.OriginalSeriesNumber!, databaseName);
                }
            }
            if (multipleWorksheet is { } multipleSheet)
            {
                var rows = ParseWorksheetCvMultiplePayment(multipleSheet);
                var existing = await sourceDb.Set<MultipleCheckVoucherPayment>().Where(x => ids.Contains(x.CheckVoucherHeaderPaymentId)).ToListAsync(token);
                var invoiceIds = existing.Select(x => x.CheckVoucherHeaderInvoiceId).ToList();
                var invoices = await sourceDb.Set<CheckVoucherHeader>().Where(x => invoiceIds.Contains(x.CheckVoucherHeaderId))
                    .ToDictionaryAsync(x => x.CheckVoucherHeaderId, x => x.OriginalDocumentId, token);
                foreach (var header in headers)
                {
                    var before = existing.Where(x => x.CheckVoucherHeaderPaymentId == header.CheckVoucherHeaderId).Select(x => new CvMultiplePaymentUploadExcelFileViewModel
                    {
                        CheckVoucherHeaderPaymentId = header.OriginalDocumentId,
                        CheckVoucherHeaderInvoiceId = invoices.GetValueOrDefault(x.CheckVoucherHeaderInvoiceId, x.CheckVoucherHeaderInvoiceId), AmountPaid = x.AmountPaid
                    });
                    var changes = DetectMultiplePayments(before, rows.Where(x => x.CheckVoucherHeaderPaymentId == header.OriginalDocumentId));
                    if (changes.Count > 0) await LogChangesAsync(header.OriginalDocumentId, changes, uploadedBy, header.OriginalSeriesNumber!, databaseName);
                }
            }
        }

        public Dictionary<string, (string Original, string New)> DetectTradePayments(
            IEnumerable<CvTradePaymentUploadExcelFileViewModel> existing, IEnumerable<CvTradePaymentUploadExcelFileViewModel> uploaded)
        {
            var before = existing.OrderBy(x => x.DocumentId).ThenBy(x => x.DocumentType).ThenBy(x => x.AmountPaid).ToList();
            var after = uploaded.OrderBy(x => x.DocumentId).ThenBy(x => x.DocumentType).ThenBy(x => x.AmountPaid).ToList();
            var changes = new Dictionary<string, (string, string)>();
            _generalRepo.Compare(changes, [], "CheckVoucherTradePayments.DocumentId",
                string.Join(",", before.Select(x => x.DocumentId)),
                string.Join(",", after.Select(x => x.DocumentId)));
            _generalRepo.Compare(changes, [], "CheckVoucherTradePayments.DocumentType",
                string.Join(",", before.Select(x => x.DocumentType)),
                string.Join(",", after.Select(x => x.DocumentType)));
            _generalRepo.Compare(changes, [], "CheckVoucherTradePayments.CheckVoucherId",
                string.Join(",", before.Select(x => x.CheckVoucherId)),
                string.Join(",", after.Select(x => x.CheckVoucherId)));
            _generalRepo.Compare(changes, [], "CheckVoucherTradePayments.AmountPaid",
                string.Join(",", before.Select(x => x.AmountPaid.ToString(CS.Four_Decimal_Format))),
                string.Join(",", after.Select(x => x.AmountPaid.ToString(CS.Four_Decimal_Format))));
            return changes;
        }

        public Dictionary<string, (string Original, string New)> DetectMultiplePayments(
            IEnumerable<CvMultiplePaymentUploadExcelFileViewModel> existing, IEnumerable<CvMultiplePaymentUploadExcelFileViewModel> uploaded)
        {
            var before = existing.OrderBy(x => x.CheckVoucherHeaderInvoiceId).ThenBy(x => x.AmountPaid).ToList();
            var after = uploaded.OrderBy(x => x.CheckVoucherHeaderInvoiceId).ThenBy(x => x.AmountPaid).ToList();
            var changes = new Dictionary<string, (string, string)>();
            _generalRepo.Compare(changes, [], "MultipleCheckVoucherPayments.CheckVoucherHeaderPaymentId",
                string.Join(",", before.Select(x => x.CheckVoucherHeaderPaymentId)),
                string.Join(",", after.Select(x => x.CheckVoucherHeaderPaymentId)));
            _generalRepo.Compare(changes, [], "MultipleCheckVoucherPayments.CheckVoucherHeaderInvoiceId",
                string.Join(",", before.Select(x => x.CheckVoucherHeaderInvoiceId)),
                string.Join(",", after.Select(x => x.CheckVoucherHeaderInvoiceId)));
            _generalRepo.Compare(changes, [], "MultipleCheckVoucherPayments.AmountPaid",
                string.Join(",", before.Select(x => x.AmountPaid.ToString(CS.Four_Decimal_Format))),
                string.Join(",", after.Select(x => x.AmountPaid.ToString(CS.Four_Decimal_Format))));
            return changes;
        }

        public IReadOnlyList<CvTradePaymentUploadExcelFileViewModel> ParseWorksheetCvTradePayment(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<CvTradePaymentUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new CvTradePaymentUploadExcelFileViewModel
                {
                    DocumentId = sheet.Value<int>(row, "DocumentId"),
                    DocumentType = sheet.HasColumn("DocumentType") ? sheet.Text(row, "DocumentType") : "RR",
                    CheckVoucherId = sheet.Value<int>(row, "CheckVoucherId"),
                    AmountPaid = sheet.Value<decimal>(row, "AmountPaid"),
                });
            }

            if (rows.Any(x => x.DocumentType != "RR"))
                throw new InvalidOperationException("Trade payments must reference DocumentType RR.");
            return rows;
        }

        public async Task<FindCvTradePaymentInDbContextDto> BuildLookupCvTradePaymentContextAsync(
            IEnumerable<CvTradePaymentUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var receivingReportIds = rows.Select(r => r.DocumentId).Distinct().ToList();
            var checkVoucherIds = rows.Select(r => r.CheckVoucherId).Distinct().ToList();

            return new FindCvTradePaymentInDbContextDto
            {
                ReceivingReportId = await _dbContext.ReceivingReports
                    .Where(x => receivingReportIds.Contains(x.OriginalDocumentId!))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId!, x => x.ReceivingReportId, cancellationToken),

                CheckVoucherHeaderId = await _dbContext.CheckVoucherHeaders
                    .Where(x => checkVoucherIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken)
            };
        }

        public CVTradePayment MapToCvTradePaymentEntity(
            CvTradePaymentUploadExcelFileViewModel row,
            FindCvTradePaymentInDbContextDto context)
        {
            return new CVTradePayment
            {
                DocumentId = context.ReceivingReportId.TryGetValue(row.DocumentId, out var receivingReportId)
                    ? receivingReportId
                    : 0,
                DocumentType = "RR",
                CheckVoucherId = context.CheckVoucherHeaderId.TryGetValue(row.CheckVoucherId, out var checkVoucherHeaderId)
                    ? checkVoucherHeaderId
                    : 0,
                AmountPaid = row.AmountPaid
            };
        }

        public IReadOnlyList<CvMultiplePaymentUploadExcelFileViewModel> ParseWorksheetCvMultiplePayment(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<CvMultiplePaymentUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new CvMultiplePaymentUploadExcelFileViewModel
                {
                    Id = Guid.NewGuid(),
                    CheckVoucherHeaderPaymentId = sheet.Value<int>(row, "CheckVoucherHeaderPaymentId"),
                    CheckVoucherHeaderInvoiceId = sheet.Value<int>(row, "CheckVoucherHeaderInvoiceId"),
                    AmountPaid = sheet.Value<decimal>(row, "AmountPaid")
                });
            }

            return rows;
        }

        public async Task<FindCvMultiplePaymentInDbContextDto> BuildLookupCvMultiplePaymentContextAsync(
            IEnumerable<CvMultiplePaymentUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var checkVoucherHeaderPaymentIds = rows.Select(r => r.CheckVoucherHeaderPaymentId).Distinct().ToList();
            var checkVoucherHeaderInvoiceIds = rows.Select(r => r.CheckVoucherHeaderInvoiceId).Distinct().ToList();

            return new FindCvMultiplePaymentInDbContextDto
            {

                CheckVoucherHeaderPaymentId =  await _dbContext.CheckVoucherHeaders
                    .Where(x => checkVoucherHeaderPaymentIds.Contains(x.OriginalDocumentId!))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken),

                CheckVoucherHeaderInvoiceId = await _dbContext.CheckVoucherHeaders
                    .Where(x => checkVoucherHeaderInvoiceIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken)
            };
        }

        public MultipleCheckVoucherPayment MapToCvMultiplePaymentEntity(
            CvMultiplePaymentUploadExcelFileViewModel row,
            FindCvMultiplePaymentInDbContextDto context)
        {
            return new MultipleCheckVoucherPayment
            {
                Id = row.Id,
                CheckVoucherHeaderPaymentId = context.CheckVoucherHeaderPaymentId.TryGetValue(row.CheckVoucherHeaderPaymentId, out var checkVoucherHeaderPaymentId)
                    ? checkVoucherHeaderPaymentId
                    : 0,
                CheckVoucherHeaderInvoiceId = context.CheckVoucherHeaderInvoiceId.TryGetValue(row.CheckVoucherHeaderInvoiceId, out var checkVoucherHeaderInvoiceId)
                ? checkVoucherHeaderInvoiceId
                : 0,
                AmountPaid = row.AmountPaid
            };
        }

        public IReadOnlyList<CheckVoucherDetailsUploadExcelFileViewModel> ParseWorksheetCheckVoucherDetails(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<CheckVoucherDetailsUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new CheckVoucherDetailsUploadExcelFileViewModel
                {
                    SuppliedOptionalFields = new[] { "Amount", "AmountPaid", "SupplierId", "EwtPercent", "IsUserSelected", "IsVatable" }
                        .Where(sheet.HasColumn).ToHashSet(),
                    TransactionNo = sheet.HasColumn("TransactionNo") ? sheet.Text(row, "TransactionNo") : null,
                    AccountNo = sheet.Text(row, "AccountNo"),
                    AccountName = sheet.Text(row, "AccountName"),
                    Debit = sheet.Value<decimal>(row, "Debit"),
                    Credit = sheet.Value<decimal>(row, "Credit"),
                    CvHeaderId = sheet.Value<int>(row, "CVHeaderId", "CheckVoucherHeaderId"),
                    OriginalDocumentId = sheet.Value<int>(row, "OriginalDocumentId"),
                    Amount = sheet.OptionalValue<decimal>(row, "Amount"),
                    AmountPaid = sheet.OptionalValue<decimal>(row, "AmountPaid"),
                    SupplierId = sheet.OptionalValue<int>(row, "SupplierId"),
                    EwtPercent = sheet.OptionalValue<decimal>(row, "EwtPercent"),
                    IsUserSelected = sheet.OptionalValue<bool>(row, "IsUserSelected"),
                    IsVatable = sheet.OptionalValue<bool>(row, "IsVatable")
                });
            }

            return rows;
        }

        public async Task<FindCheckVoucherDetailsInDbContextDto> BuildLookupCheckVoucherDetailsContextAsync(
            IEnumerable<CheckVoucherDetailsUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var cvHeaderIds = rows.Select(r => r.CvHeaderId).Distinct().ToList();
            var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
            var originalDocumentId = rows.Select(r => r.OriginalDocumentId).Distinct().ToList();

            return new FindCheckVoucherDetailsInDbContextDto
            {
                ExistingCheckVoucherDetail = await _dbContext.CheckVoucherDetails
                    .Include(x => x.CheckVoucherHeader).Include(x => x.Supplier)
                    .Where(x => x.OriginalDocumentId.HasValue && originalDocumentId.Contains(x.OriginalDocumentId!.Value))
                    .GroupBy(x => x.OriginalDocumentId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId!.Value, cancellationToken),

                SupplierId =  await _dbContext.Suppliers
                    .Where(x => x.OriginalSupplierId.HasValue && supplierIds.Contains(x.OriginalSupplierId.Value))
                    .GroupBy(x => x.OriginalSupplierId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSupplierId!.Value, x => x.SupplierId, cancellationToken),

                CheckVoucherHeader = await _dbContext.CheckVoucherHeaders
                    .Where(x => cvHeaderIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, cancellationToken)
            };
        }

        public CheckVoucherDetail MapToCheckVoucherDetailsEntity(
            CheckVoucherDetailsUploadExcelFileViewModel row,
            FindCheckVoucherDetailsInDbContextDto context)
        {
            context.ExistingCheckVoucherDetail.TryGetValue(row.OriginalDocumentId, out var existing);
            bool Has(string field) => row.SuppliedOptionalFields?.Contains(field) != false;
            return new CheckVoucherDetail
            {
                AccountNo = row.AccountNo,
                AccountName = row.AccountName,
                Debit = row.Debit,
                Credit = row.Credit,
                CheckVoucherHeaderId = context.CheckVoucherHeader.TryGetValue(row.CvHeaderId, out var checkVouchHeader)
                    ? checkVouchHeader.CheckVoucherHeaderId
                    : null,
                OriginalDocumentId = row.OriginalDocumentId,
                Amount = Has("Amount") ? row.Amount : existing?.Amount ?? default,
                AmountPaid = Has("AmountPaid") ? row.AmountPaid : existing?.AmountPaid ?? default,
                SupplierId = !Has("SupplierId") ? existing?.SupplierId : context.SupplierId.TryGetValue(row.SupplierId, out var supplierId)
                    ? supplierId
                    : null,
                EwtPercent = Has("EwtPercent") ? row.EwtPercent : existing?.EwtPercent ?? default,
                IsUserSelected = Has("IsUserSelected") ? row.IsUserSelected : existing?.IsUserSelected ?? default,
                IsVatable = Has("IsVatable") ? row.IsVatable : existing?.IsVatable ?? default,
                TransactionNo = checkVouchHeader!.CheckVoucherHeaderNo
            };
        }

        public Dictionary<string, (string Original, string New)> DetectCvDetails(
            CheckVoucherDetail entity,
            CheckVoucherDetailsUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "AccountNo",
                StringHelper.NormalizeString(entity.AccountNo),
                StringHelper.NormalizeString(row.AccountNo));

            _generalRepo.Compare(changes, logs, "AccountName",
                StringHelper.NormalizeString(entity.AccountName),
                StringHelper.NormalizeString(row.AccountName));

            _generalRepo.Compare(changes, logs, "Debit",
                entity.Debit.ToString(CS.Four_Decimal_Format),
                row.Debit.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Credit",
                entity.Credit.ToString(CS.Four_Decimal_Format),
                row.Credit.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "CheckVoucherHeaderId",
                entity.CheckVoucherHeader?.OriginalDocumentId.ToString() ?? "null",
                row.CvHeaderId.ToString());

            _generalRepo.Compare(changes, logs, "OriginalDocumentId",
                entity.OriginalDocumentId.ToString() ?? "null",
                row.OriginalDocumentId.ToString());

            if (row.SuppliedOptionalFields?.Contains("Amount") != false)
                _generalRepo.Compare(changes, logs, "Amount",
                entity.Amount.ToString(CS.Four_Decimal_Format),
                row.Amount.ToString(CS.Four_Decimal_Format));

            if (row.SuppliedOptionalFields?.Contains("EwtPercent") != false)
                _generalRepo.Compare(changes, logs, "EwtPercent",
                entity.EwtPercent.ToString(CS.Four_Decimal_Format),
                row.EwtPercent.ToString(CS.Four_Decimal_Format));

            if (row.SuppliedOptionalFields?.Contains("IsUserSelected") != false)
                _generalRepo.Compare(changes, logs, "IsUserSelected",
                entity.IsUserSelected.ToString(),
                row.IsUserSelected.ToString());

            if (row.SuppliedOptionalFields?.Contains("IsVatable") != false)
                _generalRepo.Compare(changes, logs, "IsVatable",
                entity.IsVatable.ToString(),
                row.IsVatable.ToString());

            if (row.SuppliedOptionalFields?.Contains("SupplierId") != false)
                _generalRepo.Compare(changes, logs, "SupplierId",
                (entity.Supplier?.OriginalSupplierId ?? 0).ToString(), row.SupplierId.ToString());

            if (row.TransactionNo != null)
                _generalRepo.Compare(changes, logs, "TransactionNo",
                    StringHelper.NormalizeString(entity.CheckVoucherHeader?.OriginalSeriesNumber ?? entity.TransactionNo), row.TransactionNo);

            return changes;
        }

        public async Task<FindCheckVoucherInDbContextDto> BuildLookupCheckVoucherHeaderContextForAasAsync(
            IEnumerable<CheckVoucherUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalSupplierIds = rows.Select(r => r.OriginalSupplierId).Distinct().ToList();
            var originalBankIds = rows.Select(r => r.OriginalBankId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindCheckVoucherInDbContextDto
            {
                ExistingCheckVoucherHeader = await _aasDbContext.CheckVoucherHeaders
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber!))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber!, cancellationToken),

                SupplierId = await _aasDbContext.Suppliers
                    .Where(x => originalSupplierIds.Contains(x.OriginalSupplierId!.Value))
                    .GroupBy(x => x.OriginalSupplierId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSupplierId!.Value, x => x.SupplierId, cancellationToken),

                BankId = await _aasDbContext.BankAccounts
                    .Where(x => originalBankIds.Contains(x.OriginalBankId!.Value))
                    .GroupBy(x => x.OriginalBankId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalBankId!.Value, x => x.BankAccountId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public async Task<FindCvTradePaymentInDbContextDto> BuildLookupCvTradePaymentContextForAasAsync(
            IEnumerable<CvTradePaymentUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var receivingReportIds = rows.Select(r => r.DocumentId).Distinct().ToList();
            var checkVoucherIds = rows.Select(r => r.CheckVoucherId).Distinct().ToList();

            return new FindCvTradePaymentInDbContextDto
            {
                ReceivingReportId = await _aasDbContext.ReceivingReports
                    .Where(x => receivingReportIds.Contains(x.OriginalDocumentId!))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId!, x => x.ReceivingReportId, cancellationToken),

                CheckVoucherHeaderId = await _aasDbContext.CheckVoucherHeaders
                    .Where(x => checkVoucherIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken)
            };
        }

        public async Task<FindCvMultiplePaymentInDbContextDto> BuildLookupCvMultiplePaymentContextForAasAsync(
            IEnumerable<CvMultiplePaymentUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var checkVoucherHeaderPaymentIds = rows.Select(r => r.CheckVoucherHeaderPaymentId).Distinct().ToList();
            var checkVoucherHeaderInvoiceIds = rows.Select(r => r.CheckVoucherHeaderInvoiceId).Distinct().ToList();

            return new FindCvMultiplePaymentInDbContextDto
            {

                CheckVoucherHeaderPaymentId =  await _aasDbContext.CheckVoucherHeaders
                    .Where(x => checkVoucherHeaderPaymentIds.Contains(x.OriginalDocumentId!))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken),

                CheckVoucherHeaderInvoiceId = await _aasDbContext.CheckVoucherHeaders
                    .Where(x => checkVoucherHeaderInvoiceIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken)
            };
        }

        public async Task<FindCheckVoucherDetailsInDbContextDto> BuildLookupCheckVoucherDetailsContextForAasAsync(
            IEnumerable<CheckVoucherDetailsUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var cvHeaderIds = rows.Select(r => r.CvHeaderId).Distinct().ToList();
            var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
            var originalDocumentId = rows.Select(r => r.OriginalDocumentId).Distinct().ToList();

            return new FindCheckVoucherDetailsInDbContextDto
            {
                ExistingCheckVoucherDetail = await _aasDbContext.CheckVoucherDetails
                    .Include(x => x.CheckVoucherHeader).Include(x => x.Supplier)
                    .Where(x => x.OriginalDocumentId.HasValue && originalDocumentId.Contains(x.OriginalDocumentId!.Value))
                    .GroupBy(x => x.OriginalDocumentId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId!.Value, cancellationToken),

                SupplierId =  await _aasDbContext.Suppliers
                    .Where(x => x.OriginalSupplierId.HasValue && supplierIds.Contains(x.OriginalSupplierId.Value))
                    .GroupBy(x => x.OriginalSupplierId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSupplierId!.Value, x => x.SupplierId, cancellationToken),

                CheckVoucherHeader = await _aasDbContext.CheckVoucherHeaders
                    .Where(x => cvHeaderIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, cancellationToken)
            };
        }

        public async Task GeneralLedgerBookPosting(CheckVoucherHeader header, CancellationToken cancellationToken)
        {
            var modelDetails = await _dbContext.CheckVoucherDetails
                .Where(cvd => cvd.CheckVoucherHeaderId == header.CheckVoucherHeaderId)
                .ToListAsync(cancellationToken);

            var accountTitlesDto = await _generalRepo.GetListOfAccountTitleDto(cancellationToken);
            var ledgers = new List<GeneralLedgerBook>();
            foreach (var details in modelDetails)
            {
                var account = accountTitlesDto.Find(c => c.AccountNumber == details.AccountNo) ?? throw new ArgumentException($"Account title '{details.AccountNo}' not found.");
                ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = header.Date,
                            Reference = header.CheckVoucherHeaderNo!,
                            Description = header.Particulars!,
                            AccountNo = account.AccountNumber,
                            AccountTitle = account.AccountName,
                            Debit = details.Debit,
                            Credit = details.Credit,
                            CreatedBy = header.CreatedBy,
                            CreatedDate = header.CreatedDate,
                        }
                    );
            }

            if (!_generalRepo.IsJournalEntriesBalanced(ledgers))
            {
                throw new ArgumentException("Debit and Credit is not equal, check your entries.");
            }

            await _dbContext.GeneralLedgerBooks.AddRangeAsync(ledgers, cancellationToken);
        }

        public async Task DisbursementBookPosting(CheckVoucherHeader header, CancellationToken cancellationToken)
        {
            var modelDetails = await _dbContext.CheckVoucherDetails
                .Where(cvd => cvd.CheckVoucherHeaderId == header.CheckVoucherHeaderId)
                .ToListAsync(cancellationToken);

            var supplierName = await _dbContext.Suppliers
                .Where(s => s.SupplierId == header.SupplierId)
                .Select(s => s.SupplierName)
                .FirstOrDefaultAsync(cancellationToken);

            var disbursement = new List<DisbursementBook>();
            foreach (var details in modelDetails)
            {
                var bank = _dbContext.BankAccounts.FirstOrDefault(model => model.BankAccountId == header.BankId);
                disbursement.Add(
                        new DisbursementBook
                        {
                            Date = header.Date,
                            CVNo = header.CheckVoucherHeaderNo!,
                            Payee = header.Payee ?? supplierName!,
                            Amount = header.Total,
                            Particulars = header.Particulars!,
                            Bank = bank != null ? bank.Bank : "N/A",
                            CheckNo = !string.IsNullOrEmpty(header.CheckNo) ? header.CheckNo : "N/A",
                            CheckDate = header.CheckDate != null ? header.CheckDate?.ToString("MM/dd/yyyy")! : "N/A",
                            ChartOfAccount = details.AccountNo + " " + details.AccountName,
                            Debit = details.Debit,
                            Credit = details.Credit,
                            CreatedBy = header.CreatedBy,
                            CreatedDate = header.CreatedDate
                        }
                    );
            }

            await _dbContext.DisbursementBooks.AddRangeAsync(disbursement, cancellationToken);
        }
    }
}
