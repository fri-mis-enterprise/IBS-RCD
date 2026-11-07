using System.Globalization;
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
    public class ServiceInvoiceRepo
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly GeneralRepo _generalRepo;
        private readonly AasDbContext _aasDbContext;

        public ServiceInvoiceRepo(ApplicationDbContext dbContext, GeneralRepo generalRepo, AasDbContext aasDbContext)
        {
            _dbContext = dbContext;
            _generalRepo = generalRepo;
            _aasDbContext = aasDbContext;
        }

        public async Task<List<ServiceInvoice>> GetServiceInvoicesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext
                .ServiceInvoices
                .Include(sv => sv.Customer)
                .Include(sv => sv.Service)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GenerateSvNo(CancellationToken cancellationToken = default)
        {
            var serviceInvoice = await _dbContext
                .ServiceInvoices
                .OrderByDescending(s => s.ServiceInvoiceNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (serviceInvoice != null)
            {
                string lastSeries = serviceInvoice.ServiceInvoiceNo!;
                string numericPart = lastSeries.Substring(2);
                int incrementedNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0,2) + incrementedNumber.ToString("D10");
            }
            else
            {
                return $"SV{1.ToString("D10")}";
            }
        }

        public async Task<ServiceInvoice> FindSv(int id, CancellationToken cancellationToken = default)
        {
            var serviceInvoice = await _dbContext
                .ServiceInvoices
                .Include(s => s.Customer)
                .Include(s => s.Service)
                .FirstOrDefaultAsync(s => s.ServiceInvoiceId == id, cancellationToken);

            if (serviceInvoice != null)
            {
                return serviceInvoice;
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

        public async Task<Customer> FindCustomerAsync(int? id, CancellationToken cancellationToken = default)
        {
            var customer = await _dbContext
                .Customers
                .FirstOrDefaultAsync(s => s.CustomerId == id, cancellationToken);

            if (customer != null)
            {
                return customer;
            }
            else
            {
                throw new ArgumentException("Invalid id value. The id must be greater than 0.");
            }
        }

        public async Task LogChangesAsync(int id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            var logReport = new List<ImportExportLog>();

            foreach (var change in changes)
            {
                logReport.Add(
                    new ImportExportLog
                    {
                    Id = Guid.NewGuid(),
                    TableName = nameof(DynamicView.ServiceInvoice),
                    DocumentRecordId = id,
                    ColumnName = change.Key,
                    Module = "Service Invoice",
                    OriginalValue = change.Value.OriginalValue,
                    AdjustedValue = change.Value.NewValue,
                    TimeStamp = DateTime.Now,
                    UploadedBy = modifiedBy,
                    Action = string.Empty,
                    Executed = false,
                    DocumentNo = seriesNumber,
                    DatabaseName = databaseName
                });
            }
            await _dbContext.AddRangeAsync(logReport);
        }

        public IReadOnlyList<ServiceInvoiceUploadExcelFileViewModel> ParseWorksheet(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<ServiceInvoiceUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new ServiceInvoiceUploadExcelFileViewModel
                {
                    ServiceInvoiceNo = sheet.Text(row, "OriginalSVNo", "OriginalSeriesNumber", "ServiceInvoiceNo"),
                    DueDate = DateOnly.FromDateTime(sheet.Date(row, "DueDate", true)),
                    Period = DateOnly.FromDateTime(sheet.Date(row, "Period", true)),
                    Amount = sheet.Value<decimal>(row, "Amount"),
                    Total = sheet.Value<decimal>(row, "Total"),
                    Discount = sheet.Value<decimal>(row, "Discount"),
                    CurrentAndPreviousAmount = sheet.Value<decimal>(row, "CurrentAndPreviousAmount", "CurrentAndPreviousMonth"),
                    UnearnedAmount = sheet.Value<decimal>(row, "UnearnedAmount"),
                    Status = sheet.Text(row, "Status"),
                    Instructions = sheet.Text(row, "Instructions"),
                    CwtPercent = sheet.HasColumn("CwtPercent") ? sheet.Value<decimal>(row, "CwtPercent") : 0.01m,
                    CwVatPercent = sheet.HasColumn("CwVatPercent") ? sheet.Value<decimal>(row, "CwVatPercent") : 0.05m,
                    CreatedBy = sheet.Text(row, "CreatedBy"),
                    CreatedDate = sheet.Date(row, "CreatedDate", true),
                    PostedBy = sheet.Text(row, "PostedBy"),
                    PostedDate = sheet.Date(row, "PostedDate"),
                    CancellationRemarks = sheet.Text(row, "CancellationRemarks"),
                    OriginalCustomerId = sheet.Value<int>(row, "OriginalCustomerId"),
                    OriginalSeriesNumber = sheet.Text(row, "OriginalSVNo", "OriginalSeriesNumber", "ServiceInvoiceNo"),
                    OriginalServicesId = sheet.Value<int>(row, "OriginalServicesId"),
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

        public async Task<FindServiceInvoiceInDbContextDto> BuildLookupServiceInvoiceContextAsync(
            IEnumerable<ServiceInvoiceUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalCustomerIds = rows.Select(r => r.OriginalCustomerId).Distinct().ToList();
            var originalServicesIds = rows.Select(r => r.OriginalServicesId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindServiceInvoiceInDbContextDto
            {
                ExistingInvoices = await _dbContext.ServiceInvoices
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                CustomerId = await _dbContext.Customers
                    .Where(x => originalCustomerIds.Contains(x.OriginalCustomerId))
                    .GroupBy(x => x.OriginalCustomerId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalCustomerId, x => x.CustomerId, cancellationToken),

                ServicesId = await _dbContext.Services
                    .Where(x => x.OriginalServiceId.HasValue && originalServicesIds.Contains(x.OriginalServiceId.Value))
                    .GroupBy(x => x.OriginalServiceId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalServiceId!.Value, x => x.ServiceId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public ServiceInvoice MapToServiceInvoiceEntity(
            ServiceInvoiceUploadExcelFileViewModel row,
            FindServiceInvoiceInDbContextDto context)
        {
            return new ServiceInvoice
            {
                ServiceInvoiceNo = row.ServiceInvoiceNo,
                DueDate = row.DueDate,
                Period = row.Period,
                Amount = row.Amount,
                Total = row.Total,
                Discount = row.Discount,
                CurrentAndPreviousAmount = row.CurrentAndPreviousAmount,
                UnearnedAmount = row.UnearnedAmount,
                Status = row.Status,
                Instructions = row.Instructions,
                CwtPercent = row.CwtPercent,
                CwVatPercent = row.CwVatPercent,
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
                OriginalCustomerId = row.OriginalCustomerId,
                OriginalSeriesNumber = row.OriginalSeriesNumber,
                OriginalServicesId = row.OriginalServicesId,
                OriginalDocumentId = row.OriginalDocumentId,

                CustomerId = context.CustomerId.TryGetValue(row.OriginalCustomerId, out var cId)
                    ? cId
                    : throw new InvalidOperationException($"Customer id missing for SV#{row.ServiceInvoiceNo}."),

                ServicesId = context.ServicesId.TryGetValue(row.OriginalServicesId, out var pId)
                    ? pId
                    : throw new InvalidOperationException($"Service id missing for SV#{row.ServiceInvoiceNo}.")
            };
        }

        public IEnumerable<AuditTrail> AuditTrails(
            ServiceInvoiceUploadExcelFileViewModel row,
            string machineName)
        {
            var audits = new List<AuditTrail>();

            if (!string.IsNullOrWhiteSpace(row.CreatedBy))
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CreatedBy,
                    Activity = $"Create new service invoice# {row.ServiceInvoiceNo}",
                    DocumentType = "Service Invoice",
                    MachineName = machineName,
                    Date = row.CreatedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.PostedBy) && row.PostedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.PostedBy,
                    Activity = $"Posted service invoice# {row.ServiceInvoiceNo}",
                    DocumentType = "Service Invoice",
                    MachineName = machineName,
                    Date = row.PostedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.CanceledBy) && row.CanceledDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CanceledBy,
                    Activity = $"Cancelled service invoice# {row.ServiceInvoiceNo}",
                    DocumentType = "Service Invoice",
                    MachineName = machineName,
                    Date = row.CanceledDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.VoidedBy) && row.VoidedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.VoidedBy,
                    Activity = $"Voided service invoice# {row.ServiceInvoiceNo}",
                    DocumentType = "Service Invoice",
                    MachineName = machineName,
                    Date = row.VoidedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.EditedBy) && row.EditedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.EditedBy,
                    Activity = $"Edited service invoice# {row.ServiceInvoiceNo}",
                    DocumentType = "Service Invoice",
                    MachineName = machineName,
                    Date = row.EditedDate
                });
            }

            return audits;
        }

        public Dictionary<string, (string Original, string New)> Detect(
            ServiceInvoice entity,
            ServiceInvoiceUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "CwtPercent", entity.CwtPercent.ToString("0.0000", CultureInfo.InvariantCulture), row.CwtPercent.ToString("0.0000", CultureInfo.InvariantCulture));
            _generalRepo.Compare(changes, logs, "CwVatPercent", entity.CwVatPercent.ToString("0.0000", CultureInfo.InvariantCulture), row.CwVatPercent.ToString("0.0000", CultureInfo.InvariantCulture));
            _generalRepo.Compare(changes, logs, "ServiceInvoiceNo",
                StringHelper.NormalizeString(entity.ServiceInvoiceNo),
                row.ServiceInvoiceNo);

            _generalRepo.Compare(changes, logs, "DueDate",
                entity.DueDate.ToString(CS.DateOnly_Format_For_Validation),
                row.DueDate.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "Period",
                entity.Period.ToString(CS.DateOnly_Format_For_Validation),
                row.Period.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "Amount",
                entity.Amount.ToString(CS.Four_Decimal_Format),
                row.Amount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Total",
                entity.Total.ToString(CS.Four_Decimal_Format),
                row.Total.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Discount",
                entity.Discount.ToString(CS.Four_Decimal_Format),
                row.Discount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "CurrentAndPreviousAmount",
                entity.CurrentAndPreviousAmount.ToString(CS.Four_Decimal_Format),
                row.CurrentAndPreviousAmount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "UnearnedAmount",
                entity.UnearnedAmount.ToString(CS.Four_Decimal_Format),
                row.UnearnedAmount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Status",
                StringHelper.NormalizeString(entity.Status),
                row.Status);

            _generalRepo.Compare(changes, logs, "Instructions",
                StringHelper.NormalizeString(entity.Instructions),
                row.Instructions);

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
                StringHelper.NormalizeString(entity.PostedDate?.ToString(CS.ImportDateTimeFormat)),
                row.PostedDate.ToString(CS.ImportDateTimeFormat));

            _generalRepo.Compare(changes, logs, "CancellationRemarks",
                StringHelper.NormalizeString(entity.CancellationRemarks),
                row.CancellationRemarks);

            _generalRepo.Compare(changes, logs, "OriginalCustomerId",
                StringHelper.NormalizeString(entity.OriginalCustomerId.ToString()),
                StringHelper.NormalizeString(row.OriginalCustomerId.ToString()));

            _generalRepo.Compare(changes, logs, "OriginalSeriesNumber",
                StringHelper.NormalizeString(entity.OriginalSeriesNumber),
                row.OriginalSeriesNumber);

            _generalRepo.Compare(changes, logs, "OriginalServicesId",
                StringHelper.NormalizeString(entity.OriginalServicesId.ToString()),
                StringHelper.NormalizeString(row.OriginalServicesId.ToString()));

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

        public async Task<FindServiceInvoiceInDbContextDto> BuildLookupServiceInvoiceContextForAasAsync(
            IEnumerable<ServiceInvoiceUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalCustomerIds = rows.Select(r => r.OriginalCustomerId).Distinct().ToList();
            var originalServicesIds = rows.Select(r => r.OriginalServicesId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindServiceInvoiceInDbContextDto
            {
                ExistingInvoices = await _aasDbContext.ServiceInvoices
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                CustomerId = await _aasDbContext.Customers
                    .Where(x => originalCustomerIds.Contains(x.OriginalCustomerId))
                    .GroupBy(x => x.OriginalCustomerId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalCustomerId, x => x.CustomerId, cancellationToken),

                ServicesId = await _aasDbContext.Services
                    .Where(x => x.OriginalServiceId.HasValue && originalServicesIds.Contains(x.OriginalServiceId.Value))
                    .GroupBy(x => x.OriginalServiceId!.Value)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalServiceId!.Value, x => x.ServiceId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public async Task SalesBookPosting(
            ServiceInvoiceUploadExcelFileViewModel row,
            CancellationToken cancellationToken)
        {
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.OriginalCustomerId == row.OriginalCustomerId, cancellationToken);
            var service = await _dbContext.Services.FirstOrDefaultAsync(x => x.OriginalServiceId == row.OriginalServicesId, cancellationToken);
            var serviceInvoice = await _dbContext.ServiceInvoices.FirstOrDefaultAsync(x => x.OriginalSeriesNumber == row.OriginalSeriesNumber, cancellationToken);

            var postedDate = DateOnly.FromDateTime(row.CreatedDate) >= row.Period ? DateOnly.FromDateTime(row.CreatedDate) : row.Period.AddMonths(1).AddDays(-1);

            decimal netOfVatAmount;
            decimal vatAmount = 0;

            if (customer!.VatType == CS.VatType_Vatable)
            {
                netOfVatAmount = _generalRepo.ComputeNetOfVat(row.Total);
                vatAmount = _generalRepo.ComputeVatAmount(netOfVatAmount);
            }
            else
            {
                netOfVatAmount = row.Total;
            }

            var sales = new SalesBook();

            if (customer.VatType == "Vatable")
            {
                sales.TransactionDate = postedDate;
                sales.SerialNo = row.ServiceInvoiceNo!;
                sales.SoldTo = customer.CustomerName;
                sales.TinNo = customer.CustomerTin;
                sales.Address = customer.CustomerAddress;
                sales.Description = service!.Name;
                sales.Amount = row.Total;
                sales.VatAmount = vatAmount;
                sales.VatableSales = netOfVatAmount;
                sales.Discount = row.Discount;
                sales.NetSales = netOfVatAmount;
                sales.CreatedBy = row.CreatedBy;
                sales.CreatedDate = row.CreatedDate;
                sales.DueDate = row.DueDate;
                sales.DocumentId = serviceInvoice?.ServiceInvoiceId;
            }
            else if (customer.VatType == "Exempt")
            {
                sales.TransactionDate = postedDate;
                sales.SerialNo = row.ServiceInvoiceNo!;
                sales.SoldTo = customer.CustomerName;
                sales.TinNo = customer.CustomerTin;
                sales.Address = customer.CustomerAddress;
                sales.Description = service!.Name;
                sales.Amount = row.Total;
                sales.VatExemptSales = row.Total;
                sales.Discount = row.Discount;
                sales.NetSales = netOfVatAmount;
                sales.CreatedBy = row.CreatedBy;
                sales.CreatedDate = row.CreatedDate;
                sales.DueDate = row.DueDate;
                sales.DocumentId = serviceInvoice?.ServiceInvoiceId;
            }
            else
            {
                sales.TransactionDate = postedDate;
                sales.SerialNo = row.ServiceInvoiceNo!;
                sales.SoldTo = customer.CustomerName;
                sales.TinNo = customer.CustomerTin;
                sales.Address = customer.CustomerAddress;
                sales.Description = service!.Name;
                sales.Amount = row.Total;
                sales.ZeroRated = row.Total;
                sales.Discount = row.Discount;
                sales.NetSales = netOfVatAmount;
                sales.CreatedBy = row.CreatedBy;
                sales.CreatedDate = row.CreatedDate;
                sales.DueDate = row.DueDate;
                sales.DocumentId = serviceInvoice?.ServiceInvoiceId;
            }

            await _dbContext.SalesBooks.AddAsync(sales, cancellationToken);
        }

        public async Task GeneralLedgerBookPosting(
            ServiceInvoiceUploadExcelFileViewModel row,
            CancellationToken cancellationToken)
        {
            var customer = _dbContext.Customers.FirstOrDefault(x => x.OriginalCustomerId == row.OriginalCustomerId);
            var service = _dbContext.Services.FirstOrDefault(x => x.OriginalServiceId == row.OriginalServicesId);

            decimal withHoldingTaxAmount = 0;
            decimal withHoldingVatAmount = 0;
            decimal netOfVatAmount;
            decimal vatAmount = 0;

            if (customer!.VatType == CS.VatType_Vatable)
            {
                netOfVatAmount = _generalRepo.ComputeNetOfVat(row.Total);
                vatAmount = _generalRepo.ComputeVatAmount(netOfVatAmount);
            }
            else
            {
                netOfVatAmount = row.Total;
            }

            if (customer.WithHoldingTax)
            {
                withHoldingTaxAmount = _generalRepo.ComputeEwtAmount(netOfVatAmount, row.CwtPercent);
            }

            if (customer.WithHoldingVat)
            {
                withHoldingVatAmount = _generalRepo.ComputeEwtAmount(netOfVatAmount, row.CwVatPercent);
            }

            var postedDate = DateOnly.FromDateTime(row.CreatedDate) >= row.Period ? DateOnly.FromDateTime(row.CreatedDate) : row.Period.AddMonths(1).AddDays(-1);

            var ledgers = new List<GeneralLedgerBook>();
            var accountTitlesDto = await _generalRepo.GetListOfAccountTitleDto(cancellationToken);
            var arNonTradeTitle = accountTitlesDto.Find(c => c.AccountNumber == "101020500") ?? throw new ArgumentException("Account title '101020500' not found.");
            var arTradeCwt = accountTitlesDto.Find(c => c.AccountNumber == "101020200") ?? throw new ArgumentException("Account title '101020200' not found.");
            var arTradeCwv = accountTitlesDto.Find(c => c.AccountNumber == "101020300") ?? throw new ArgumentException("Account title '101020300' not found.");
            var vatOutputTitle = accountTitlesDto.Find(c => c.AccountNumber == "201030100") ?? throw new ArgumentException("Account title '201030100' not found.");

            ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = postedDate,
                        Reference = row.ServiceInvoiceNo!,
                        Description = service!.Name,
                        AccountNo = arNonTradeTitle.AccountNumber,
                        AccountTitle = arNonTradeTitle.AccountName,
                        Debit = Math.Round(row.Total - (withHoldingTaxAmount + withHoldingVatAmount), 2),
                        Credit = 0,
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    }
                );
            if (withHoldingTaxAmount > 0)
            {
                ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = postedDate,
                        Reference = row.ServiceInvoiceNo!,
                        Description = service!.Name,
                        AccountNo = arTradeCwt.AccountNumber,
                        AccountTitle = arTradeCwt.AccountName,
                        Debit = withHoldingTaxAmount,
                        Credit = 0,
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    }
                );
            }
            if (withHoldingVatAmount > 0)
            {
                ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = postedDate,
                        Reference = row.ServiceInvoiceNo!,
                        Description = service!.Name,
                        AccountNo = arTradeCwv.AccountNumber,
                        AccountTitle = arTradeCwv.AccountName,
                        Debit = withHoldingVatAmount,
                        Credit = 0,
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    }
                );
            }

            ledgers.Add(
                   new GeneralLedgerBook
                   {
                       Date = postedDate,
                       Reference = row.ServiceInvoiceNo!,
                       Description = service!.Name,
                       AccountNo = service.CurrentAndPreviousNo!,
                       AccountTitle = service.CurrentAndPreviousTitle!,
                       Debit = 0,
                       Credit = Math.Round((netOfVatAmount), 2),
                       CreatedBy = row.CreatedBy,
                       CreatedDate = row.CreatedDate
                   }
               );

            if (vatAmount > 0)
            {
                ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = postedDate,
                        Reference = row.ServiceInvoiceNo!,
                        Description = service!.Name,
                        AccountNo = vatOutputTitle.AccountNumber,
                        AccountTitle = vatOutputTitle.AccountName,
                        Debit = 0,
                        Credit = Math.Round((vatAmount), 2),
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
