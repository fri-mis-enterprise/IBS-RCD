using Accounting_System.Data;
using Accounting_System.DTOs;
using Accounting_System.Models;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.Reports;
using Accounting_System.Models.ViewModels;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository
{
    public class CreditMemoRepo
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly GeneralRepo _generalRepo;
        private readonly AasDbContext _aasDbContext;

        public CreditMemoRepo(ApplicationDbContext dbContext, GeneralRepo generalRepo, AasDbContext aasDbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _generalRepo = generalRepo;
            _aasDbContext = aasDbContext;
        }

        public async Task<string> GenerateCMNo(CancellationToken cancellationToken = default)
        {
            var creditMemo = await _dbContext
                .CreditMemos
                .OrderByDescending(s => s.CreditMemoNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (creditMemo != null)
            {
                string lastSeries = creditMemo.CreditMemoNo ?? throw new InvalidOperationException("CMNo is null pls Contact MIS Enterprise");
                string numericPart = lastSeries.Substring(2);
                int incrementNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0,2) + incrementNumber.ToString("D10");
            }
            else
            {
                return $"CM{1.ToString("D10")}";
            }
        }

        public async Task CalculateTaxAmountsAsync(CreditMemo model, CancellationToken cancellationToken = default)
        {
            if (model.SalesInvoiceId.HasValue == model.ServiceInvoiceId.HasValue)
            {
                throw new ArgumentException("Select exactly one sales or service invoice.");
            }

            // Credit memos are negative amounts; compute on the absolute value then negate.
            var creditAmount = -Math.Abs(model.CreditAmount);
            if (model.SalesInvoiceId.HasValue)
            {
                var salesInvoice = await _dbContext.SalesInvoices
                    .Include(invoice => invoice.Customer)
                    .FirstOrDefaultAsync(invoice => invoice.SalesInvoiceId == model.SalesInvoiceId, cancellationToken)
                    ?? throw new ArgumentException("Sales invoice not found.");
                var vatType = salesInvoice.Customer?.VatType?.Trim().Replace(" ", "-", StringComparison.Ordinal);
                var hasEwt = salesInvoice.Customer?.WithHoldingTax ?? false;
                var hasWvat = salesInvoice.Customer?.WithHoldingVat ?? false;
                var netOfVatAmount = vatType == CS.VatType_Vatable
                    ? (creditAmount != 0m ? -_generalRepo.ComputeNetOfVat(Math.Abs(creditAmount)) : 0m)
                    : Math.Round(creditAmount, 4, MidpointRounding.AwayFromZero);

                model.CwtAmount = hasEwt && netOfVatAmount != 0m && salesInvoice.CwtPercent > 0m
                    ? _generalRepo.ComputeEwtAmount(Math.Abs(netOfVatAmount), salesInvoice.CwtPercent) * -1
                    : 0m;
                model.CwVatAmount = hasWvat && netOfVatAmount != 0m && salesInvoice.CwVatPercent > 0m
                    ? _generalRepo.ComputeEwtAmount(Math.Abs(netOfVatAmount), salesInvoice.CwVatPercent) * -1
                    : 0m;
                return;
            }

            var serviceInvoice = await _dbContext.ServiceInvoices
                .Include(invoice => invoice.Customer)
                .FirstOrDefaultAsync(invoice => invoice.ServiceInvoiceId == model.ServiceInvoiceId, cancellationToken)
                ?? throw new ArgumentException("Service invoice not found.");
            // Legacy/imported ServiceInvoices have empty VatType; fall back to Customer.
            var rawServiceVatType = string.IsNullOrWhiteSpace(serviceInvoice.VatType) ? serviceInvoice.Customer?.VatType : serviceInvoice.VatType;
            var serviceVatType = rawServiceVatType?.Trim().Replace(" ", "-", StringComparison.Ordinal) ?? string.Empty;
            var serviceNetOfVatAmount = serviceVatType == CS.VatType_Vatable
                ? (creditAmount != 0m ? -_generalRepo.ComputeNetOfVat(Math.Abs(creditAmount)) : 0m)
                : Math.Round(creditAmount, 4, MidpointRounding.AwayFromZero);

            model.CwtAmount = serviceInvoice.HasEwt && serviceNetOfVatAmount != 0m && serviceInvoice.ServicePercent > 0m
                ? _generalRepo.ComputeEwtAmount(Math.Abs(serviceNetOfVatAmount), serviceInvoice.ServicePercent / 100m) * -1
                : 0m;
            model.CwVatAmount = serviceInvoice.HasWvat && serviceNetOfVatAmount != 0m
                ? _generalRepo.ComputeEwtAmount(Math.Abs(serviceNetOfVatAmount), 0.05m) * -1
                : 0m;
        }

        public async Task<string> GetSINoAsync(int? id, CancellationToken cancellationToken = default)
        {
            if (id != 0)
            {
                var si = await _dbContext
                                .SalesInvoices
                                .FirstOrDefaultAsync(po => po.SalesInvoiceId == id, cancellationToken);
                return si!.SalesInvoiceNo!;
            }

            throw new ArgumentException("No record found in supplier.");
        }

        public async Task<string> GetSVNoAsync(int? id, CancellationToken cancellationToken = default)
        {
            if (id != 0)
            {
                var sv = await _dbContext
                                .ServiceInvoices
                                .FirstOrDefaultAsync(po => po.ServiceInvoiceId == id, cancellationToken);
                return sv!.ServiceInvoiceNo!;
            }

            throw new ArgumentException("No record found in supplier.");
        }

        public async Task<CreditMemo> FindCM(int id, CancellationToken cancellationToken = default)
        {
            var creditMemo = await _dbContext
                .CreditMemos
                .Include(c => c.SalesInvoice)
                .ThenInclude(s => s!.Product)
                .Include(c => c.SalesInvoice)
                .ThenInclude(s => s!.Customer)
                .Include(c => c.ServiceInvoice)
                .ThenInclude(sv => sv!.Customer)
                .Include(c => c.ServiceInvoice)
                .ThenInclude(sv => sv!.Service)
                .FirstOrDefaultAsync(creditMemo => creditMemo.CreditMemoId == id, cancellationToken);

            if (creditMemo != null)
            {
                return creditMemo;
            }
            else
            {
                throw new ArgumentException("Invalid id value. The id must be greater than 0.");
            }
        }

        public async Task<Services> GetServicesAsync(int? id, CancellationToken cancellationToken = default)
        {
            var services = await _dbContext
                .Services
                .FirstOrDefaultAsync(s => s.ServiceId == id, cancellationToken);

            if (services != null)
            {
                return services;
            }
            else
            {
                throw new ArgumentException("Invalid id value. The id must be greater than 0.");
            }
        }
        public async Task<List<CreditMemo>> GetCreditMemosAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.CreditMemos
                .Include(cm => cm.SalesInvoice)
                .ThenInclude(s => s!.Customer)
                .Include(cm => cm.SalesInvoice)
                .ThenInclude(s => s!.Product)
                .Include(cm => cm.ServiceInvoice)
                .ThenInclude(sv => sv!.Customer)
                .Include(cm => cm.ServiceInvoice)
                .ThenInclude(sv => sv!.Service)
                .ToListAsync(cancellationToken);
        }

        public async Task LogChangesAsync(int id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            foreach (var change in changes)
            {
                var logReport = new ImportExportLog()
                {
                    Id = Guid.NewGuid(),
                    TableName = nameof(DynamicView.CreditMemo),
                    DocumentRecordId = id,
                    ColumnName = change.Key,
                    Module = "Credit Memo",
                    OriginalValue = change.Value.OriginalValue,
                    AdjustedValue = change.Value.NewValue,
                    TimeStamp = DateTime.Now,
                    UploadedBy = modifiedBy,
                    Action = string.Empty,
                    Executed = false,
                    DocumentNo = seriesNumber,
                    DatabaseName = databaseName
                };
                await _dbContext.AddAsync(logReport);
            }
        }

        public IReadOnlyList<CreditMemoUploadExcelFileViewModel> ParseWorksheet(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<CreditMemoUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new CreditMemoUploadExcelFileViewModel
                {
                    CreditMemoNo = sheet.Text(row, "OriginalCMNo", "OriginalSeriesNumber", "CreditMemoNo"),
                    TransactionDate = DateOnly.FromDateTime(sheet.Date(row, "TransactionDate", true)),
                    CreditAmount = sheet.Value<decimal>(row, "CreditAmount", "DebitAmount"),
                    Description = sheet.Text(row, "Description"),
                    AdjustedPrice = sheet.Value<decimal>(row, "AdjustedPrice"),
                    Quantity = sheet.Value<decimal>(row, "Quantity"),
                    Source = sheet.Text(row, "Source"),
                    Remarks = sheet.Text(row, "Remarks"),
                    Period = DateOnly.FromDateTime(sheet.Date(row, "Period", true)),
                    Amount = sheet.Value<decimal>(row, "Amount"),
                    CurrentAndPreviousAmount = sheet.Value<decimal>(row, "CurrentAndPreviousAmount"),
                    UnearnedAmount = sheet.Value<decimal>(row, "UnearnedAmount"),
                    ServicesId = sheet.Value<int>(row, "ServicesId"),
                    CreatedBy = sheet.Text(row, "CreatedBy"),
                    CreatedDate = sheet.Date(row, "CreatedDate", true),
                    PostedBy = sheet.Text(row, "PostedBy"),
                    PostedDate = sheet.Date(row, "PostedDate"),
                    CancellationRemarks = sheet.Text(row, "CancellationRemarks"),
                    OriginalSalesInvoiceId = sheet.Value<int>(row, "OriginalSalesInvoiceId"),
                    OriginalSeriesNumber = sheet.Text(row, "OriginalCMNo", "OriginalSeriesNumber", "CreditMemoNo"),
                    OriginalServiceInvoiceId = sheet.Value<int>(row, "OriginalServiceInvoiceId"),
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

        public async Task<FindCreditMemoInDbContextDto> BuildLookupCreditMemoContextAsync(
            IEnumerable<CreditMemoUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalSalesInvoiceIds = rows.Select(r => r.OriginalSalesInvoiceId).Distinct().ToList();
            var originalServiceInvoicess = rows.Select(r => r.OriginalServiceInvoiceId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindCreditMemoInDbContextDto
            {
                ExistingCreditMemo = await _dbContext.CreditMemos
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                SalesInvoiceId = await _dbContext.SalesInvoices
                    .Where(x => originalSalesInvoiceIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.SalesInvoiceId, cancellationToken),

                ServiceInvoiceId = await _dbContext.ServiceInvoices
                    .Where(x => originalServiceInvoicess.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.ServiceInvoiceId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public CreditMemo MapToCreditMemoEntity(
            CreditMemoUploadExcelFileViewModel row,
            FindCreditMemoInDbContextDto context)
        {
            return new CreditMemo
            {
                CreditMemoNo = row.CreditMemoNo,
                TransactionDate = row.TransactionDate,
                CreditAmount = row.CreditAmount,
                Description = row.Description,
                AdjustedPrice = row.AdjustedPrice,
                Quantity = row.Quantity,
                Source = row.Source,
                Remarks = row.Remarks,
                Period = row.Period,
                Amount = row.Amount,
                CurrentAndPreviousAmount = row.CurrentAndPreviousAmount,
                UnearnedAmount = row.UnearnedAmount,
                ServicesId = row.ServicesId,
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
                OriginalSalesInvoiceId = row.OriginalSalesInvoiceId,
                OriginalSeriesNumber = row.OriginalSeriesNumber,
                OriginalServiceInvoiceId = row.OriginalServiceInvoiceId,
                OriginalDocumentId = row.OriginalDocumentId,

                SalesInvoiceId = context.SalesInvoiceId.TryGetValue(row.OriginalSalesInvoiceId, out var cId)
                    ? cId
                    : null,

                ServiceInvoiceId = context.ServiceInvoiceId.TryGetValue(row.OriginalServiceInvoiceId, out var pId)
                    ? pId
                    : null
            };
        }

        public IEnumerable<AuditTrail> AuditTrails(
            CreditMemoUploadExcelFileViewModel row,
            string machineName)
        {
            var audits = new List<AuditTrail>();

            if (!string.IsNullOrWhiteSpace(row.CreatedBy))
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CreatedBy,
                    Activity = $"Create new credit memo# {row.CreditMemoNo}",
                    DocumentType = "Credit Memo",
                    MachineName = machineName,
                    Date = row.CreatedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.PostedBy) && row.PostedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.PostedBy,
                    Activity = $"Posted credit memo# {row.CreditMemoNo}",
                    DocumentType = "Credit Memo",
                    MachineName = machineName,
                    Date = row.PostedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.CanceledBy) && row.CanceledDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CanceledBy,
                    Activity = $"Cancelled credit memo# {row.CreditMemoNo}",
                    DocumentType = "Credit Memo",
                    MachineName = machineName,
                    Date = row.CanceledDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.VoidedBy) && row.VoidedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.VoidedBy,
                    Activity = $"Voided credit memo# {row.CreditMemoNo}",
                    DocumentType = "Credit Memo",
                    MachineName = machineName,
                    Date = row.VoidedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.EditedBy) && row.EditedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.EditedBy,
                    Activity = $"Edited credit memo# {row.CreditMemoNo}",
                    DocumentType = "Credit Memo",
                    MachineName = machineName,
                    Date = row.EditedDate
                });
            }

            return audits;
        }

        public Dictionary<string, (string Original, string New)> Detect(
            CreditMemo entity,
            CreditMemoUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "CreditMemoNo",
                StringHelper.NormalizeString(entity.CreditMemoNo),
                row.CreditMemoNo);

            _generalRepo.Compare(changes, logs, "TransactionDate",
                entity.TransactionDate.ToString(CS.DateOnly_Format_For_Validation),
                row.TransactionDate.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "CreditAmount",
                entity.CreditAmount.ToString(CS.Four_Decimal_Format),
                row.CreditAmount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Description",
                StringHelper.NormalizeString(entity.Description),
                row.Description);

            _generalRepo.Compare(changes, logs, "AdjustedPrice",
                entity.AdjustedPrice?.ToString(CS.Four_Decimal_Format) ?? "0.0000",
                row.AdjustedPrice.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Quantity",
                entity.Quantity?.ToString(CS.Four_Decimal_Format) ?? "0.0000",
                row.Quantity.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Source",
                StringHelper.NormalizeString(entity.Source),
                row.Source);

            _generalRepo.Compare(changes, logs, "Remarks",
                StringHelper.NormalizeString(entity.Remarks),
                row.Remarks);

            _generalRepo.Compare(changes, logs, "Period",
                entity.Period.ToString(CS.DateOnly_Format_For_Validation),
                row.Period.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "Amount",
                entity.Amount?.ToString(CS.Four_Decimal_Format) ?? "0.0000",
                row.Amount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "CurrentAndPreviousAmount",
                entity.CurrentAndPreviousAmount.ToString(CS.Four_Decimal_Format),
                row.CurrentAndPreviousAmount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "UnearnedAmount",
                entity.UnearnedAmount.ToString(CS.Four_Decimal_Format),
                row.UnearnedAmount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "ServicesId",
                StringHelper.NormalizeString(entity.ServicesId.ToString()),
                StringHelper.NormalizeString(row.ServicesId.ToString()));

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

            _generalRepo.Compare(changes, logs, "OriginalSalesInvoiceId",
                StringHelper.NormalizeString(entity.OriginalSalesInvoiceId.ToString()),
                StringHelper.NormalizeString(row.OriginalSalesInvoiceId.ToString()));

            _generalRepo.Compare(changes, logs, "OriginalSeriesNumber",
                StringHelper.NormalizeString(entity.OriginalSeriesNumber),
                StringHelper.NormalizeString(row.OriginalSeriesNumber));

            _generalRepo.Compare(changes, logs, "OriginalServiceInvoiceId",
                StringHelper.NormalizeString(entity.OriginalServiceInvoiceId.ToString()),
                StringHelper.NormalizeString(row.OriginalServiceInvoiceId.ToString()));

            _generalRepo.Compare(changes, logs, "OriginalDocumentId",
                StringHelper.NormalizeString(entity.OriginalDocumentId.ToString()),
                StringHelper.NormalizeString(row.OriginalDocumentId.ToString()));

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

        public async Task<FindCreditMemoInDbContextDto> BuildLookupCreditMemoContextForAasAsync(
            IEnumerable<CreditMemoUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalSalesInvoiceIds = rows.Select(r => r.OriginalSalesInvoiceId).Distinct().ToList();
            var originalServiceInvoicess = rows.Select(r => r.OriginalServiceInvoiceId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindCreditMemoInDbContextDto
            {
                ExistingCreditMemo = await _aasDbContext.CreditMemos
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                SalesInvoiceId = await _aasDbContext.SalesInvoices
                    .Where(x => originalSalesInvoiceIds.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.SalesInvoiceId, cancellationToken),

                ServiceInvoiceId = await _aasDbContext.ServiceInvoices
                    .Where(x => originalServiceInvoicess.Contains(x.OriginalDocumentId))
                    .GroupBy(x => x.OriginalDocumentId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalDocumentId, x => x.ServiceInvoiceId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        private ViewModelDMCM ComputeSvComputation(
            CreditMemoUploadExcelFileViewModel row,
            Customer customer,
            Services service)
        {
            var viewModelDmcm = new ViewModelDMCM();

            viewModelDmcm.Period = DateOnly.FromDateTime(row.CreatedDate) >= row.Period ? DateOnly.FromDateTime(row.CreatedDate) : row.Period.AddMonths(1).AddDays(-1);

            if (customer.VatType == "Vatable")
            {
                viewModelDmcm.Total = -row.Amount;
                viewModelDmcm.NetAmount = row.Amount / 1.12m;
                viewModelDmcm.VatAmount = row.Amount - viewModelDmcm.NetAmount;
                viewModelDmcm.WithholdingTaxAmount = viewModelDmcm.NetAmount * (service.Percent / 100m);
                if (customer.WithHoldingVat)
                {
                    viewModelDmcm.WithholdingVatAmount = viewModelDmcm.NetAmount * customer.CwVatPercent;
                }
            }
            else
            {
                viewModelDmcm.NetAmount = row.Amount;
                viewModelDmcm.WithholdingTaxAmount = viewModelDmcm.NetAmount * (service.Percent / 100m);
                if (customer.WithHoldingVat)
                {
                    viewModelDmcm.WithholdingVatAmount = viewModelDmcm.NetAmount * customer.CwVatPercent;
                }
            }

            if (customer.VatType == "Vatable")
            {
                var total = Math.Round(row.Amount, 2);

                var roundedNetAmount = Math.Round(viewModelDmcm.NetAmount, 2);

                if (roundedNetAmount > total)
                {
                    var shortAmount = viewModelDmcm.NetAmount - total;

                    viewModelDmcm.Amount += shortAmount;
                }
            }

            return viewModelDmcm;
        }

        public async Task SalesBookPosting(
            CreditMemoUploadExcelFileViewModel row,
            CancellationToken cancellationToken)
        {
            var salesInvoice = await _dbContext.SalesInvoices
                .Include(x => x.Customer)
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.OriginalDocumentId == row.OriginalSalesInvoiceId, cancellationToken);

            var serviceInvoice = await _dbContext.ServiceInvoices
                .Include(x => x.Customer)
                .Include(x => x.Service)
                .FirstOrDefaultAsync(x => x.OriginalDocumentId == row.OriginalServiceInvoiceId, cancellationToken);

            if (salesInvoice != null)
            {
                var customer = salesInvoice.Customer!;
                var product = salesInvoice.Product!;
                var sales = new SalesBook();

                if (customer.VatType == "Vatable")
                {
                    sales.TransactionDate = row.TransactionDate;
                    sales.SerialNo = row.CreditMemoNo;
                    sales.SoldTo = customer.CustomerName;
                    sales.TinNo = customer.CustomerTin;
                    sales.Address = customer.CustomerAddress;
                    sales.Description = product.ProductName;
                    sales.Amount = row.CreditAmount;
                    sales.VatableSales = (_generalRepo.ComputeNetOfVat(Math.Abs(sales.Amount))) * -1;
                    sales.VatAmount = (_generalRepo.ComputeVatAmount(Math.Abs(sales.VatableSales))) * -1;
                    sales.NetSales = sales.VatableSales;
                    sales.CreatedBy = row.CreatedBy;
                    sales.CreatedDate = row.CreatedDate;
                    sales.DueDate = salesInvoice.DueDate;
                    sales.DocumentId = salesInvoice.SalesInvoiceId;
                }
                else if (customer.VatType == "Exempt")
                {
                    sales.TransactionDate = row.TransactionDate;
                    sales.SerialNo = row.CreditMemoNo;
                    sales.SoldTo = customer.CustomerName;
                    sales.TinNo = customer.CustomerTin;
                    sales.Address = customer.CustomerAddress;
                    sales.Description = product.ProductName;
                    sales.Amount = row.CreditAmount;
                    sales.VatExemptSales = row.CreditAmount;
                    sales.NetSales = sales.Amount;
                    sales.CreatedBy = row.CreatedBy;
                    sales.CreatedDate = row.CreatedDate;
                    sales.DueDate = salesInvoice.DueDate;
                    sales.DocumentId = salesInvoice.SalesInvoiceId;
                }
                else
                {
                    sales.TransactionDate = row.TransactionDate;
                    sales.SerialNo = row.CreditMemoNo;
                    sales.SoldTo = customer.CustomerName;
                    sales.TinNo = customer.CustomerTin;
                    sales.Address = customer.CustomerAddress;
                    sales.Description = product.ProductName;
                    sales.Amount = row.CreditAmount;
                    sales.ZeroRated = row.CreditAmount;
                    sales.NetSales = sales.Amount;
                    sales.CreatedBy = row.CreatedBy;
                    sales.CreatedDate = row.CreatedDate;
                    sales.DueDate = salesInvoice.DueDate;
                    sales.DocumentId = salesInvoice.SalesInvoiceId;
                }

                await _dbContext.SalesBooks.AddAsync(sales, cancellationToken);
            }

            if (serviceInvoice != null)
            {
                var customer = serviceInvoice.Customer!;
                var service = serviceInvoice.Service!;
                var viewModelDmcm = ComputeSvComputation(row, customer, service);
                var sales = new SalesBook();

                if (customer.VatType == "Vatable")
                {
                    sales.TransactionDate = viewModelDmcm.Period;
                    sales.SerialNo = row.CreditMemoNo;
                    sales.SoldTo = customer.CustomerName;
                    sales.TinNo = customer.CustomerTin;
                    sales.Address = customer.CustomerAddress;
                    sales.Description = service.Name;
                    sales.Amount = viewModelDmcm.Total;
                    sales.VatableSales = (_generalRepo.ComputeNetOfVat(Math.Abs(sales.Amount))) * -1;
                    sales.VatAmount = (_generalRepo.ComputeVatAmount(Math.Abs(sales.VatableSales))) * -1;
                    sales.NetSales = viewModelDmcm.NetAmount;
                    sales.CreatedBy = row.CreatedBy;
                    sales.CreatedDate = row.CreatedDate;
                    sales.DueDate = serviceInvoice.DueDate;
                    sales.DocumentId = serviceInvoice.ServiceInvoiceId;
                }
                else if (customer.VatType == "Exempt")
                {
                    sales.TransactionDate = viewModelDmcm.Period;
                    sales.SerialNo = row.CreditMemoNo;
                    sales.SoldTo = customer.CustomerName;
                    sales.TinNo = customer.CustomerTin;
                    sales.Address = customer.CustomerAddress;
                    sales.Description = service.Name;
                    sales.Amount = viewModelDmcm.Total;
                    sales.VatExemptSales = viewModelDmcm.Total;
                    sales.NetSales = viewModelDmcm.NetAmount;
                    sales.CreatedBy = row.CreatedBy;
                    sales.CreatedDate = row.CreatedDate;
                    sales.DueDate = serviceInvoice.DueDate;
                    sales.DocumentId = serviceInvoice.ServiceInvoiceId;
                }
                else
                {
                    sales.TransactionDate = viewModelDmcm.Period;
                    sales.SerialNo = row.CreditMemoNo;
                    sales.SoldTo = customer.CustomerName;
                    sales.TinNo = customer.CustomerTin;
                    sales.Address = customer.CustomerAddress;
                    sales.Description = service.Name;
                    sales.Amount = viewModelDmcm.Total;
                    sales.ZeroRated = viewModelDmcm.Total;
                    sales.NetSales = viewModelDmcm.NetAmount;
                    sales.CreatedBy = row.CreatedBy;
                    sales.CreatedDate = row.CreatedDate;
                    sales.DueDate = serviceInvoice.DueDate;
                    sales.DocumentId = serviceInvoice.ServiceInvoiceId;
                }

                await _dbContext.SalesBooks.AddAsync(sales, cancellationToken);
            }
        }

        public async Task GeneralLedgerBookPosting(
            CreditMemoUploadExcelFileViewModel row,
            CancellationToken cancellationToken)
        {
            var salesInvoice = await _dbContext.SalesInvoices
                .Include(x => x.Customer)
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.OriginalDocumentId == row.OriginalSalesInvoiceId, cancellationToken);

            var serviceInvoice = await _dbContext.ServiceInvoices
                .Include(x => x.Customer)
                .Include(x => x.Service)
                .FirstOrDefaultAsync(x => x.OriginalDocumentId == row.OriginalServiceInvoiceId, cancellationToken);

            var accountTitlesDto = await _generalRepo.GetListOfAccountTitleDto(cancellationToken);
            var arTradeReceivableTitle = accountTitlesDto.Find(c => c.AccountNumber == "101020100") ?? throw new ArgumentException("Account number: '101020100', Account title: 'AR-Trade Receivable' not found.");
            var arNonTradeReceivableTitle = accountTitlesDto.Find(c => c.AccountNumber == "101020500") ?? throw new ArgumentException("Account number: '101020500', Account title: 'AR-Non Trade Receivable' not found.");
            var arTradeCwt = accountTitlesDto.Find(c => c.AccountNumber == "101020200") ?? throw new ArgumentException("Account number: '101020200', Account title: 'AR-Trade Receivable - Creditable Withholding Tax' not found.");
            var arTradeCwv = accountTitlesDto.Find(c => c.AccountNumber == "101020300") ?? throw new ArgumentException("Account number: '101020300', Account title: 'AR-Trade Receivable - Creditable Withholding Vat' not found.");
            var vatOutputTitle = accountTitlesDto.Find(c => c.AccountNumber == "201030100") ?? throw new ArgumentException("Account number: '201030100', Account title: 'Vat - Output' not found.");

            if (salesInvoice != null)
            {
                var customer = salesInvoice.Customer!;
                var product = salesInvoice.Product!;
                var (salesAcctNo, _) = _generalRepo.GetSalesAccountTitle(product.ProductCode!);
                var salesTitle = accountTitlesDto.Find(c => c.AccountNumber == salesAcctNo) ?? throw new ArgumentException($"Account title '{salesAcctNo}' not found.");

                decimal withHoldingTaxAmount = 0;
                decimal withHoldingVatAmount = 0;
                decimal netOfVatAmount;
                decimal vatAmount = 0;
                if (customer.VatType == CS.VatType_Vatable)
                {
                    netOfVatAmount = (_generalRepo.ComputeNetOfVat(Math.Abs(row.CreditAmount))) * -1;
                    vatAmount = (_generalRepo.ComputeVatAmount(Math.Abs(netOfVatAmount))) * -1;
                }
                else
                {
                    netOfVatAmount = row.CreditAmount;
                }
                if (customer.WithHoldingTax)
                {
                    withHoldingTaxAmount = (_generalRepo.ComputeEwtAmount(Math.Abs(netOfVatAmount), salesInvoice.CwtPercent)) * -1;
                }
                if (customer.WithHoldingVat)
                {
                    withHoldingVatAmount = (_generalRepo.ComputeEwtAmount(Math.Abs(netOfVatAmount), salesInvoice.CwVatPercent)) * -1;
                }

                var ledgers = new List<GeneralLedgerBook>
                {
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.CreditMemoNo,
                        Description = product.ProductName,
                        AccountNo = arTradeReceivableTitle.AccountNumber,
                        AccountTitle = arTradeReceivableTitle.AccountName,
                        Debit = 0,
                        Credit = Math.Abs(row.CreditAmount - (withHoldingTaxAmount + withHoldingVatAmount)),
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    }
                };

                if (withHoldingTaxAmount < 0)
                {
                    ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = row.TransactionDate,
                            Reference = row.CreditMemoNo,
                            Description = product.ProductName,
                            AccountNo = arTradeCwt.AccountNumber,
                            AccountTitle = arTradeCwt.AccountName,
                            Debit = 0,
                            Credit = Math.Abs(withHoldingTaxAmount),
                            CreatedBy = row.CreatedBy,
                            CreatedDate = row.CreatedDate
                        }
                    );
                }
                if (withHoldingVatAmount < 0)
                {
                    ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = row.TransactionDate,
                            Reference = row.CreditMemoNo,
                            Description = product.ProductName,
                            AccountNo = arTradeCwv.AccountNumber,
                            AccountTitle = arTradeCwv.AccountName,
                            Debit = 0,
                            Credit = Math.Abs(withHoldingVatAmount),
                            CreatedBy = row.CreatedBy,
                            CreatedDate = row.CreatedDate
                        }
                    );
                }
                ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.CreditMemoNo,
                        Description = product.ProductName,
                        AccountNo = salesTitle.AccountNumber,
                        AccountTitle = salesTitle.AccountName,
                        Debit = Math.Abs(netOfVatAmount),
                        CreatedBy = row.CreatedBy,
                        Credit = 0,
                        CreatedDate = row.CreatedDate
                    }
                );
                if (vatAmount < 0)
                {
                    ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = row.TransactionDate,
                            Reference = row.CreditMemoNo,
                            Description = product.ProductName,
                            AccountNo = vatOutputTitle.AccountNumber,
                            AccountTitle = vatOutputTitle.AccountName,
                            Debit = Math.Abs(vatAmount),
                            Credit = 0,
                            CreatedBy = row.CreatedBy,
                            CreatedDate = row.CreatedDate
                        }
                    );
                }

                if (!_generalRepo.IsJournalEntriesBalanced(ledgers))
                {
                    throw new ArgumentException("Debit and Credit is not equal, check your entries.");
                }

                await _dbContext.GeneralLedgerBooks.AddRangeAsync(ledgers, cancellationToken);
            }

            if (serviceInvoice != null)
            {
                var customer = serviceInvoice.Customer!;
                var service = serviceInvoice.Service!;
                var viewModelDmcm = ComputeSvComputation(row, customer, service);

                decimal withHoldingTaxAmount = 0;
                decimal withHoldingVatAmount = 0;
                decimal netOfVatAmount;
                decimal vatAmount = 0;
                if (customer.VatType == CS.VatType_Vatable)
                {
                    netOfVatAmount = (_generalRepo.ComputeNetOfVat(Math.Abs(row.CreditAmount))) * -1;
                    vatAmount = (_generalRepo.ComputeVatAmount(Math.Abs(netOfVatAmount))) * -1;
                }
                else
                {
                    netOfVatAmount = row.CreditAmount;
                }
                if (customer.WithHoldingTax)
                {
                    withHoldingTaxAmount = (_generalRepo.ComputeEwtAmount(Math.Abs(netOfVatAmount), serviceInvoice.CwtPercent)) * -1;
                }
                if (customer.WithHoldingVat)
                {
                    withHoldingVatAmount = _generalRepo.ComputeEwtAmount(Math.Abs(netOfVatAmount), serviceInvoice.CwVatPercent) * -1;
                }

                var ledgers = new List<GeneralLedgerBook>
                {
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.CreditMemoNo,
                        Description = service.Name,
                        AccountNo = arNonTradeReceivableTitle.AccountNumber,
                        AccountTitle = arNonTradeReceivableTitle.AccountName,
                        Debit = 0,
                        Credit = Math.Abs(row.CreditAmount - (withHoldingTaxAmount + withHoldingVatAmount)),
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    }
                };

                if (withHoldingTaxAmount < 0)
                {
                    ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = row.TransactionDate,
                            Reference = row.CreditMemoNo,
                            Description = service.Name,
                            AccountNo = arTradeCwt.AccountNumber,
                            AccountTitle = arTradeCwt.AccountName,
                            Debit = 0,
                            Credit = Math.Abs(withHoldingTaxAmount),
                            CreatedBy = row.CreatedBy,
                            CreatedDate = row.CreatedDate
                        }
                    );
                }
                if (withHoldingVatAmount < 0)
                {
                    ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = row.TransactionDate,
                            Reference = row.CreditMemoNo,
                            Description = service.Name,
                            AccountNo = arTradeCwv.AccountNumber,
                            AccountTitle = arTradeCwv.AccountName,
                            Debit = 0,
                            Credit = Math.Abs(withHoldingVatAmount),
                            CreatedBy = row.CreatedBy,
                            CreatedDate = row.CreatedDate
                        }
                    );
                }

                ledgers.Add(new GeneralLedgerBook
                {
                    Date = row.TransactionDate,
                    Reference = row.CreditMemoNo,
                    Description = service.Name,
                    AccountNo = service.CurrentAndPreviousNo!,
                    AccountTitle = service.CurrentAndPreviousTitle!,
                    Debit = viewModelDmcm.NetAmount,
                    Credit = 0,
                    CreatedBy = row.CreatedBy,
                    CreatedDate = row.CreatedDate
                });

                if (vatAmount < 0)
                {
                    ledgers.Add(
                        new GeneralLedgerBook
                        {
                            Date = row.TransactionDate,
                            Reference = row.CreditMemoNo,
                            Description = service.Name,
                            AccountNo = vatOutputTitle.AccountNumber,
                            AccountTitle = vatOutputTitle.AccountName,
                            Debit = Math.Abs(vatAmount),
                            Credit = 0,
                            CreatedBy = row.CreatedBy,
                            CreatedDate = row.CreatedDate
                        }
                    );
                }

                if (!_generalRepo.IsJournalEntriesBalanced(ledgers))
                {
                    throw new ArgumentException("Debit and Credit is not equal, check your entries.");
                }

                await _dbContext.GeneralLedgerBooks.AddRangeAsync(ledgers, cancellationToken);
            }
        }
    }
}
