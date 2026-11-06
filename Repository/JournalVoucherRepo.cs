using Accounting_System.Data;
using Accounting_System.DTOs;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.Reports;
using Accounting_System.Models.ViewModels;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository
{
    public class JournalVoucherRepo
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly GeneralRepo _generalRepo;

        private readonly AasDbContext _aasDbContext;

        public JournalVoucherRepo(ApplicationDbContext dbContext, GeneralRepo generalRepo, AasDbContext aasDbContext)
        {
            _dbContext = dbContext;
            _generalRepo = generalRepo;
            _aasDbContext = aasDbContext;
        }

        public async Task<List<JournalVoucherHeader>> GetJournalVouchersAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.JournalVoucherHeaders
                .Include(j => j.CheckVoucherHeader)
                .ThenInclude(cv => cv!.Supplier)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GenerateJVNo(CancellationToken cancellationToken = default)
        {
            var journalVoucher = await _dbContext
                .JournalVoucherHeaders
                .OrderByDescending(j => j.JournalVoucherHeaderNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (journalVoucher != null)
            {
                string lastSeries = journalVoucher.JournalVoucherHeaderNo ?? throw new InvalidOperationException("JVNo is null pls Contact MIS Enterprise");
                string numericPart = lastSeries.Substring(2);
                int incrementedNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0,2) + incrementedNumber.ToString("D10");
            }
            else
            {
                return $"JV{1.ToString("D10")}";
            }
        }

        public async Task LogChangesAsync(int id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            foreach (var change in changes)
            {
                var logReport = new ImportExportLog()
                {
                    Id = Guid.NewGuid(),
                    TableName = "JournalVoucherHeader",
                    DocumentRecordId = id,
                    ColumnName = change.Key,
                    Module = "Journal Voucher Header",
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

        public async Task LogChangesForJVDAsync(int? id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            foreach (var change in changes)
            {
                var logReport = new ImportExportLog()
                {
                    Id = Guid.NewGuid(),
                    TableName = "JournalVoucherDetails",
                    DocumentRecordId = id!.Value,
                    ColumnName = change.Key,
                    Module = "Journal Voucher Details",
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

        public IReadOnlyList<JournalVoucherUploadExcelFileViewModel> ParseWorksheet(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<JournalVoucherUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new JournalVoucherUploadExcelFileViewModel
                {
                    JournalVoucherHeaderNo = sheet.Text(row, "OriginalJVNo", "OriginalSeriesNumber", "JournalVoucherHeaderNo"),
                    Date = DateOnly.FromDateTime(sheet.Date(row, "TransactionDate", true, "Date")),
                    References = sheet.Text(row, "Reference", "References"),
                    Particulars = sheet.Text(row, "Particulars"),
                    CRNo = sheet.Text(row, "CRNo"),
                    JVReason = sheet.Text(row, "JVReason"),
                    CreatedBy = sheet.Text(row, "CreatedBy"),
                    CreatedDate = sheet.Date(row, "CreatedDate", true),
                    PostedBy = sheet.Text(row, "PostedBy"),
                    PostedDate = sheet.Date(row, "PostedDate"),
                    CancellationRemarks = sheet.Text(row, "CancellationRemarks"),
                    OriginalCVId = sheet.Value<int>(row, "OriginalCVId"),
                    OriginalSeriesNumber = sheet.Text(row, "OriginalJVNo", "OriginalSeriesNumber", "JournalVoucherHeaderNo"),
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

        public async Task<FindJournalVoucherInDbContextDto> BuildLookupCheckVoucherHeaderContextAsync(
            IEnumerable<JournalVoucherUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalCheckVoucherIds = rows.Select(r => r.OriginalCVId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindJournalVoucherInDbContextDto
            {
                ExistingJournalVoucherHeader = await _dbContext.JournalVoucherHeaders
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber!))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber!, cancellationToken),

                CvId = await _dbContext.CheckVoucherHeaders
                    .Where(x => originalCheckVoucherIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public async Task<FindJournalVoucherInDbContextDto> BuildLookupJournalVoucherHeaderContextForAasAsync(
            IEnumerable<JournalVoucherUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalCheckVoucherIds = rows.Select(r => r.OriginalCVId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindJournalVoucherInDbContextDto
            {
                ExistingJournalVoucherHeader = await _aasDbContext.JournalVoucherHeaders
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber!))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber!, cancellationToken),

                CvId = await _aasDbContext.CheckVoucherHeaders
                    .Where(x => originalCheckVoucherIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.CheckVoucherHeaderId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public JournalVoucherHeader MapToJournalVoucherEntity(
            JournalVoucherUploadExcelFileViewModel row,
            FindJournalVoucherInDbContextDto context)
        {
            return new JournalVoucherHeader
            {
                JournalVoucherHeaderNo = row.JournalVoucherHeaderNo,
                Date = row.Date,
                References = row.References,
                Particulars = row.Particulars,
                CRNo = row.CRNo,
                JVReason = row.JVReason,
                CreatedBy = row.CreatedBy,
                CreatedDate = row.CreatedDate,
                IsPosted = !string.IsNullOrEmpty(row.PostedBy),
                PostedBy = row.PostedBy,
                PostedDate = row.PostedDate,
                CancellationRemarks = row.CancellationRemarks,
                IsCanceled = !string.IsNullOrEmpty(row.CanceledBy),
                CanceledBy = row.CanceledBy,
                CanceledDate = row.CanceledDate,
                IsVoided = !string.IsNullOrEmpty(row.VoidedBy),
                VoidedBy = row.VoidedBy,
                VoidedDate = row.VoidedDate,
                EditedBy = row.EditedBy,
                EditedDate = row.EditedDate,
                OriginalCVId = row.OriginalCVId,
                OriginalSeriesNumber = row.OriginalSeriesNumber,
                OriginalDocumentId = row.OriginalDocumentId,

                CVId = row.OriginalCVId == 0 ? null : context.CvId.TryGetValue(row.OriginalCVId, out var cvId)
                    ? cvId
                    : throw new InvalidOperationException(
                        "Please upload the Excel file for the check voucher first.")
            };
        }

        public IEnumerable<AuditTrail> AuditTrails(
            JournalVoucherUploadExcelFileViewModel row,
            string machineName)
        {
            var audits = new List<AuditTrail>();

            if (!string.IsNullOrWhiteSpace(row.CreatedBy))
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CreatedBy,
                    Activity = $"Create new journal vouchcer# {row.JournalVoucherHeaderNo}",
                    DocumentType = "Journal Voucher",
                    MachineName = machineName,
                    Date = row.CreatedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.PostedBy) && row.PostedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.PostedBy,
                    Activity = $"Posted journal voucher# {row.JournalVoucherHeaderNo}",
                    DocumentType = "Journal Voucher",
                    MachineName = machineName,
                    Date = row.PostedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.CanceledBy) && row.CanceledDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CanceledBy,
                    Activity = $"Cancelled journal voucher# {row.JournalVoucherHeaderNo}",
                    DocumentType = "Journal Voucher",
                    MachineName = machineName,
                    Date = row.CanceledDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.VoidedBy) && row.VoidedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.VoidedBy,
                    Activity = $"Voided journal voucher# {row.JournalVoucherHeaderNo}",
                    DocumentType = "Journal Voucher",
                    MachineName = machineName,
                    Date = row.VoidedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.EditedBy) && row.EditedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.EditedBy,
                    Activity = $"Edited journal voucher# {row.JournalVoucherHeaderNo}",
                    DocumentType = "Journal Voucher",
                    MachineName = machineName,
                    Date = row.EditedDate
                });
            }

            return audits;
        }

        public Dictionary<string, (string Original, string New)> Detect(
            JournalVoucherHeader entity,
            JournalVoucherUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "JournalVoucherHeaderNo",
                StringHelper.NormalizeString(entity.JournalVoucherHeaderNo),
                row.JournalVoucherHeaderNo);

            _generalRepo.Compare(changes, logs, "Date",
                entity.Date.ToString(CS.DateOnly_Format_For_Validation),
                row.Date.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "References",
                StringHelper.NormalizeString(entity.References),
                row.References);

            _generalRepo.Compare(changes, logs, "Particulars",
                StringHelper.NormalizeString(entity.Particulars),
                row.Particulars);

            _generalRepo.Compare(changes, logs, "CRNo",
                StringHelper.NormalizeString(entity.CRNo),
                row.CRNo);

            _generalRepo.Compare(changes, logs, "JVReason",
                StringHelper.NormalizeString(entity.JVReason),
                row.JVReason);

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
                (entity.PostedDate ?? default).ToString(CS.ImportDateTimeFormat),
                row.PostedDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "CancellationRemarks",
                StringHelper.NormalizeString(entity.CancellationRemarks),
                row.CancellationRemarks);

            _generalRepo.Compare(changes, logs, "OriginalCVId",
                entity.OriginalCVId?.ToString() ?? "null",
                row.OriginalCVId.ToString());

            _generalRepo.Compare(changes, logs, "OriginalSeriesNumber",
                StringHelper.NormalizeString(entity.OriginalSeriesNumber),
                row.OriginalSeriesNumber);

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

            return changes;
        }

        public IReadOnlyList<JournalVoucherDetailsUploadExcelFileViewModel> ParseWorksheetJournalVoucherDetails(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<JournalVoucherDetailsUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new JournalVoucherDetailsUploadExcelFileViewModel
                {
                    AccountNo = sheet.Text(row, "AccountNo"),
                    AccountName = sheet.Text(row, "AccountName"),
                    TransactionNo = sheet.Text(row, "TransactionNo"),
                    Debit = sheet.Value<decimal>(row, "Debit"),
                    Credit = sheet.Value<decimal>(row, "Credit"),
                    JournalVoucherHeaderId = sheet.Value<int>(row, "JVHeaderId", "JournalVoucherHeaderId"),
                    OriginalDocumentId = sheet.Value<int>(row, "OriginalDocumentId"),
                });
            }

            return rows;
        }

        public async Task<FindJournalVoucherDetailsInDbContextDto> BuildLookupJournalVoucherDetailsContextAsync(
            IEnumerable<JournalVoucherDetailsUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var jvHeaderIds = rows.Select(r => r.JournalVoucherHeaderId).Distinct().ToList();
            var originalDocumentId = rows.Select(r => r.OriginalDocumentId).Distinct().ToList();

            return new FindJournalVoucherDetailsInDbContextDto
            {
                ExistingJournalVoucherDetail = await _dbContext.JournalVoucherDetails
                    .Include(x => x.JournalVoucherHeader)
                    .Where(x => x.OriginalDocumentId.HasValue && originalDocumentId.Contains(x.OriginalDocumentId!.Value))
                    .GroupBy(x => x.OriginalDocumentId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId!.Value, cancellationToken),

                JournalVoucherHeader = await _dbContext.JournalVoucherHeaders
                    .Where(x => jvHeaderIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, cancellationToken)
            };
        }

        public async Task<FindJournalVoucherDetailsInDbContextDto> BuildLookupJournalVoucherDetailsContextForAasAsync(
            IEnumerable<JournalVoucherDetailsUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var jvHeaderIds = rows.Select(r => r.JournalVoucherHeaderId).Distinct().ToList();
            var originalDocumentId = rows.Select(r => r.OriginalDocumentId).Distinct().ToList();

            return new FindJournalVoucherDetailsInDbContextDto
            {
                ExistingJournalVoucherDetail = await _aasDbContext.JournalVoucherDetails
                    .Include(x => x.JournalVoucherHeader)
                    .Where(x => x.OriginalDocumentId.HasValue && originalDocumentId.Contains(x.OriginalDocumentId!.Value))
                    .GroupBy(x => x.OriginalDocumentId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId!.Value, cancellationToken),

                JournalVoucherHeader = await _aasDbContext.JournalVoucherHeaders
                    .Where(x => jvHeaderIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalDocumentId.Contains(x.DocumentRecordId))
                    .ToListAsync(cancellationToken)
            };
        }

        public JournalVoucherDetail MapToJournalVoucherDetailsEntity(
            JournalVoucherDetailsUploadExcelFileViewModel row,
            FindJournalVoucherDetailsInDbContextDto context)
        {
            if (!context.JournalVoucherHeader.TryGetValue(row.JournalVoucherHeaderId, out var journalVoucherHeader))
            {
                throw new InvalidOperationException($"Journal voucher header id missing for JournalVoucherHeaderId #{row.JournalVoucherHeaderId}.");
            }
            return new JournalVoucherDetail
            {
                AccountNo = row.AccountNo,
                AccountName = row.AccountName,
                TransactionNo = journalVoucherHeader.JournalVoucherHeaderNo!,
                Debit = row.Debit,
                Credit = row.Credit,
                OriginalDocumentId = row.OriginalDocumentId,
                JournalVoucherHeaderId = journalVoucherHeader.JournalVoucherHeaderId
            };
        }

        public Dictionary<string, (string Original, string New)> DetectJvDetails(
            JournalVoucherDetail entity,
            JournalVoucherDetailsUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "AccountNo",
                StringHelper.NormalizeString(entity.AccountNo),
                row.AccountNo);

            _generalRepo.Compare(changes, logs, "AccountName",
                StringHelper.NormalizeString(entity.AccountName),
                row.AccountName);

            _generalRepo.Compare(changes, logs, "Debit",
                entity.Debit.ToString(CS.Four_Decimal_Format),
                row.Debit.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Credit",
                entity.Credit.ToString(CS.Four_Decimal_Format),
                row.Credit.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "OriginalDocumentId",
                entity.OriginalDocumentId?.ToString() ?? "null",
                row.OriginalDocumentId.ToString());

            _generalRepo.Compare(changes, logs, "JournalVoucherHeaderId",
                (entity.JournalVoucherHeader?.OriginalDocumentId ?? 0).ToString(), row.JournalVoucherHeaderId.ToString());
            _generalRepo.Compare(changes, logs, "TransactionNo",
                StringHelper.NormalizeString(entity.JournalVoucherHeader?.OriginalSeriesNumber ?? entity.TransactionNo), StringHelper.NormalizeString(row.TransactionNo));

            return changes;
        }

        public async Task GeneralLedgerBookPosting(JournalVoucherHeader header, CancellationToken cancellationToken)
        {
            var modelDetails = await _dbContext.JournalVoucherDetails.Where(jvd => jvd.TransactionNo == header.JournalVoucherHeaderNo).ToListAsync(cancellationToken);

            var accountTitlesDto = await _generalRepo.GetListOfAccountTitleDto(cancellationToken);
            var ledgers = new List<GeneralLedgerBook>();
            foreach (var details in modelDetails)
            {
                var account = accountTitlesDto.Find(c => c.AccountNumber == details.AccountNo) ?? throw new ArgumentException($"Account number '{details.AccountNo}', Account title '{details.AccountName}' not found.");
                ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = header.Date,
                            Reference = header.JournalVoucherHeaderNo!,
                            Description = header.Particulars,
                            AccountNo = account.AccountNumber,
                            AccountTitle = account.AccountName,
                            Debit = details.Debit,
                            Credit = details.Credit,
                            CreatedBy = header.CreatedBy,
                            CreatedDate = header.CreatedDate
                        }
                    );
            }

            if (!_generalRepo.IsJournalEntriesBalanced(ledgers))
            {
                throw new ArgumentException("Debit and Credit is not equal, check your entries.");
            }

            await _dbContext.GeneralLedgerBooks.AddRangeAsync(ledgers, cancellationToken);
        }

        public async Task JournalBookPosting(JournalVoucherHeader header, CancellationToken cancellationToken)
        {
            var modelDetails = await _dbContext.JournalVoucherDetails.Where(jvd => jvd.TransactionNo == header.JournalVoucherHeaderNo).ToListAsync(cancellationToken);

            var journalBook = new List<JournalBook>();
            foreach (var details in modelDetails)
            {
                journalBook.Add(
                        new JournalBook
                        {
                            Date = header.Date,
                            Reference = header.JournalVoucherHeaderNo!,
                            Description = header.Particulars,
                            AccountTitle = details.AccountNo + " " + details.AccountName,
                            Debit = details.Debit,
                            Credit = details.Credit,
                            CreatedBy = header.CreatedBy,
                            CreatedDate = header.CreatedDate
                        }
                    );
            }

            await _dbContext.JournalBooks.AddRangeAsync(journalBook, cancellationToken);
        }
    }
}
