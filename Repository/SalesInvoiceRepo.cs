using System.Globalization;
using Accounting_System.Data;
using Accounting_System.DTOs;
using Accounting_System.Models;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.Reports;
using Accounting_System.Models.ViewModels;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;

namespace Accounting_System.Repository
{
    public class SalesInvoiceRepo
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly GeneralRepo _generalRepo;
        private readonly AasDbContext _aasDbContext;

        public SalesInvoiceRepo(ApplicationDbContext dbContext, GeneralRepo generalRepo, AasDbContext aasDbContext)
        {
            _dbContext = dbContext;
            _generalRepo = generalRepo;
            _aasDbContext = aasDbContext;
        }

        public async Task<List<SalesInvoice>> GetSalesInvoicesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext
                .SalesInvoices
                .Include(s => s.Product)
                .Include(c => c.Customer)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GenerateSINo(CancellationToken cancellationToken = default)
        {
            var salesInvoice = await _dbContext
                .SalesInvoices
                .OrderByDescending(s => s.SalesInvoiceNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (salesInvoice != null)
            {
                string lastSeries = salesInvoice.SalesInvoiceNo!;
                string numericPart = lastSeries.Substring(2);
                int incrementedNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0,2) + incrementedNumber.ToString("D10");
            }
            else
            {
                return $"SI{1.ToString("D10")}";
            }
        }

        public async Task<SalesInvoice> FindSalesInvoice(int? id, CancellationToken cancellationToken = default)
        {
            var invoice = await _dbContext
                .SalesInvoices
                .Include(c => c.Customer)
                .Include(s => s.Product)
                .FirstOrDefaultAsync(invoice => invoice.SalesInvoiceId == id);

            if (invoice != null)
            {
                return invoice;
            }
            else
            {
                throw new ArgumentException("Invalid id value. The id must be greater than 0.");
            }
        }

        public DateOnly ComputeDueDateAsync(string customerTerms, DateOnly date, CancellationToken cancellationToken = default)
        {
            if (!customerTerms.IsNullOrEmpty())
            {
                DateOnly dueDate;

                switch (customerTerms)
                {
                    case "7D":
                        return date.AddDays(7);

                    case "10D":
                        return date.AddDays(7);

                    case "15D":
                        return date.AddDays(15);

                    case "30D":
                        return date.AddDays(30);

                    case "45D":
                    case "45PDC":
                        return date.AddDays(45);

                    case "60D":
                    case "60PDC":
                        return date.AddDays(60);

                    case "90D":
                        return date.AddDays(90);

                    case "M15":
                        return date.AddMonths(1).AddDays(15 - date.Day);

                    case "M30":
                        if (date.Month == 1)
                        {
                            dueDate = new DateOnly(date.Year, date.Month, 1).AddMonths(2).AddDays(-1);
                        }
                        else
                        {
                            dueDate = new DateOnly(date.Year, date.Month, 1).AddMonths(2).AddDays(-1);

                            if (dueDate.Day == 31)
                            {
                                dueDate = dueDate.AddDays(-1);
                            }
                        }
                        return dueDate;

                    case "M29":
                        if (date.Month == 1)
                        {
                            dueDate = new DateOnly(date.Year, date.Month, 1).AddMonths(2).AddDays(-1);
                        }
                        else
                        {
                            dueDate = new DateOnly(date.Year, date.Month, 1).AddMonths(2).AddDays(-1);

                            if (dueDate.Day == 31)
                            {
                                dueDate = dueDate.AddDays(-2);
                            }
                            else if (dueDate.Day == 30)
                            {
                                dueDate = dueDate.AddDays(-1);
                            }
                        }
                        return dueDate;

                    default:
                        return date;
                }
            }

            throw new ArgumentException("No record found.");
        }

        public async Task LogChangesAsync(int id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            foreach (var change in changes)
            {
                if (change.Key == nameof(SalesInvoice.Status) && await _dbContext.ImportExportLogs.AnyAsync(log =>
                    log.TableName == nameof(DynamicView.SalesInvoice) && log.DatabaseName == databaseName &&
                    log.DocumentRecordId == id && log.DocumentNo == seriesNumber && log.ColumnName == change.Key &&
                    log.OriginalValue == change.Value.OriginalValue && log.AdjustedValue == change.Value.NewValue &&
                    log.Action == string.Empty)) continue;
                var logReport = new ImportExportLog()
                {
                    Id = Guid.NewGuid(),
                    TableName = nameof(DynamicView.SalesInvoice),
                    DocumentRecordId = id,
                    ColumnName = change.Key,
                    Module = "Sales Invoice",
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

        public IReadOnlyList<SalesInvoiceUploadExcelFileViewModel> ParseWorksheet(
            ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<SalesInvoiceUploadExcelFileViewModel>();
            var rowCount = worksheet.Dimension.Rows;

            for (var row = 2; row <= rowCount; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new SalesInvoiceUploadExcelFileViewModel
                {
                    SalesInvoiceNo = sheet.Text(row, "OriginalSINo", "OriginalSeriesNumber", "SalesInvoiceNo"),
                    OtherRefNo = sheet.Text(row, "OtherRefNo"),

                    Quantity = sheet.RequiredValue<decimal>(row, "Quantity"),
                    UnitPrice = sheet.RequiredValue<decimal>(row, "UnitPrice"),
                    Amount = sheet.RequiredValue<decimal>(row, "Amount"),
                    Discount = sheet.Value<decimal>(row, "Discount"),

                    Remarks = sheet.Text(row, "Remarks"),
                    Status = sheet.Text(row, "Status"),

                    TransactionDate = DateOnly.FromDateTime(sheet.Date(row, "TransactionDate", true)),
                    DueDate = DateOnly.FromDateTime(sheet.Date(row, "DueDate", true)),

                    CwtPercent = sheet.HasColumn("CwtPercent") ? sheet.Value<decimal>(row, "CwtPercent") : 0.01m,
                    CwVatPercent = sheet.HasColumn("CwVatPercent") ? sheet.Value<decimal>(row, "CwVatPercent") : 0.05m,
                    CreatedBy = sheet.Text(row, "CreatedBy"),
                    CreatedDate = sheet.Date(row, "CreatedDate", true),

                    PostedBy = sheet.Text(row, "PostedBy"),
                    PostedDate = sheet.Date(row, "PostedDate"),

                    CancellationRemarks = sheet.Text(row, "CancellationRemarks"),
                    OriginalCustomerId = sheet.Value<int>(row, "OriginalCustomerId"),
                    OriginalProductId = sheet.Value<int>(row, "OriginalProductId"),
                    OriginalDocumentId = sheet.Value<int>(row, "OriginalDocumentId"),
                    OriginalSeriesNumber = sheet.Text(row, "OriginalSINo", "OriginalSeriesNumber", "SalesInvoiceNo"),
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

        public async Task<FindSalesInvoiceInDbContextDto> BuildLookupSalesInvoiceContextAsync(
            IEnumerable<SalesInvoiceUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalCustomerIds = rows.Select(r => r.OriginalCustomerId).Distinct().ToList();
            var originalProductIds = rows.Select(r => r.OriginalProductId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindSalesInvoiceInDbContextDto
            {
                ExistingInvoices = await _dbContext.SalesInvoices
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                CustomerId = await _dbContext.Customers
                    .Where(x => originalCustomerIds.Contains(x.OriginalCustomerId))
                    .GroupBy(x => x.OriginalCustomerId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalCustomerId, x => x.CustomerId, cancellationToken),

                ProductId = await _dbContext.Products
                    .Where(x => originalProductIds.Contains(x.OriginalProductId))
                    .GroupBy(x => x.OriginalProductId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalProductId, x => x.ProductId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public SalesInvoice MapToSalesInvoiceEntity(
            SalesInvoiceUploadExcelFileViewModel row,
            FindSalesInvoiceInDbContextDto context)
        {
            var existingSalesInvoice = context.ExistingInvoices.TryGetValue(row.OriginalSeriesNumber, out var invoice);
            return new SalesInvoice
            {
                SalesInvoiceNo = row.SalesInvoiceNo,
                OtherRefNo = row.OtherRefNo,
                Quantity = row.Quantity,
                UnitPrice = row.UnitPrice,
                Amount = row.Amount,
                Discount = row.Discount,

                Remarks = row.Remarks,
                Status = existingSalesInvoice ? invoice!.Status : row.Status,

                TransactionDate = row.TransactionDate,
                DueDate = row.DueDate,

                CwtPercent = row.CwtPercent,
                CwVatPercent = row.CwVatPercent,
                CreatedBy = row.CreatedBy,
                CreatedDate = row.CreatedDate,

                IsPosted = !existingSalesInvoice && !string.IsNullOrEmpty(row.PostedBy),
                PostedBy = !existingSalesInvoice ? row.PostedBy : invoice!.PostedBy,
                PostedDate = !existingSalesInvoice ? row.PostedDate : invoice!.PostedDate,

                CancellationRemarks = row.CancellationRemarks,
                IsCanceled = !existingSalesInvoice && !string.IsNullOrEmpty(row.CanceledBy),
                CanceledBy = !existingSalesInvoice ? row.CanceledBy : invoice!.CanceledBy,
                CanceledDate = !existingSalesInvoice ? row.CanceledDate: invoice!.CanceledDate,
                IsVoided = !existingSalesInvoice && !string.IsNullOrEmpty(row.VoidedBy),
                VoidedBy = !existingSalesInvoice ? row.VoidedBy : invoice!.VoidedBy,
                VoidedDate = !existingSalesInvoice ? row.VoidedDate : invoice!.VoidedDate,
                EditedBy = row.EditedBy,
                EditedDate = row.EditedDate,
                OriginalCustomerId = row.OriginalCustomerId,
                OriginalProductId = row.OriginalProductId,
                OriginalDocumentId = row.OriginalDocumentId,
                OriginalSeriesNumber = row.OriginalSeriesNumber,

                CustomerId = context.CustomerId.TryGetValue(row.OriginalCustomerId, out var cId)
                    ? cId
                    : throw new InvalidOperationException($"Customer id missing for SI#{row.SalesInvoiceNo}."),

                ProductId = context.ProductId.TryGetValue(row.OriginalProductId, out var pId)
                    ? pId
                    : throw new InvalidOperationException($"Product id missing for SI#{row.SalesInvoiceNo}.")
            };
        }

        public IEnumerable<AuditTrail> AuditTrails(
            SalesInvoiceUploadExcelFileViewModel row,
            string machineName)
        {
            var audits = new List<AuditTrail>();

            if (!string.IsNullOrWhiteSpace(row.CreatedBy))
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CreatedBy,
                    Activity = $"Create new invoice# {row.SalesInvoiceNo}",
                    DocumentType = "Sales Invoice",
                    MachineName = machineName,
                    Date = row.CreatedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.PostedBy) && row.PostedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.PostedBy,
                    Activity = $"Posted invoice# {row.SalesInvoiceNo}",
                    DocumentType = "Sales Invoice",
                    MachineName = machineName,
                    Date = row.PostedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.CanceledBy) && row.CanceledDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CanceledBy,
                    Activity = $"Cancelled invoice# {row.SalesInvoiceNo}",
                    DocumentType = "Sales Invoice",
                    MachineName = machineName,
                    Date = row.CanceledDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.VoidedBy) && row.VoidedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.VoidedBy,
                    Activity = $"Voided invoice# {row.SalesInvoiceNo}",
                    DocumentType = "Sales Invoice",
                    MachineName = machineName,
                    Date = row.VoidedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.EditedBy) && row.EditedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.EditedBy,
                    Activity = $"Edited invoice# {row.SalesInvoiceNo}",
                    DocumentType = "Sales Invoice",
                    MachineName = machineName,
                    Date = row.EditedDate
                });
            }

            return audits;
        }

        public Dictionary<string, (string Original, string New)> Detect(
            SalesInvoice entity,
            SalesInvoiceUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "CwtPercent", entity.CwtPercent.ToString("0.0000", CultureInfo.InvariantCulture), row.CwtPercent.ToString("0.0000", CultureInfo.InvariantCulture));
            _generalRepo.Compare(changes, logs, "CwVatPercent", entity.CwVatPercent.ToString("0.0000", CultureInfo.InvariantCulture), row.CwVatPercent.ToString("0.0000", CultureInfo.InvariantCulture));
            _generalRepo.Compare(changes, logs, "SalesInvoiceNo",
                StringHelper.NormalizeString(entity.SalesInvoiceNo),
                row.SalesInvoiceNo);

            _generalRepo.Compare(changes, logs, "OtherRefNo",
                StringHelper.NormalizeString(entity.OtherRefNo),
                row.OtherRefNo);

            _generalRepo.Compare(changes, logs, "Quantity",
                entity.Quantity.ToString(CS.Four_Decimal_Format),
                row.Quantity.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "UnitPrice",
                entity.UnitPrice.ToString(CS.Four_Decimal_Format),
                row.UnitPrice.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Amount",
                entity.Amount.ToString(CS.Four_Decimal_Format),
                row.Amount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Remarks",
                StringHelper.NormalizeString(entity.Remarks),
                row.Remarks);

            _generalRepo.Compare(changes, logs, "Status",
                StringHelper.NormalizeString(entity.Status),
                row.Status);

            _generalRepo.Compare(changes, logs, "TransactionDate",
                entity.TransactionDate.ToString(CS.DateOnly_Format_For_Validation),
                row.TransactionDate.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "Discount",
                entity.Discount.ToString(CS.Four_Decimal_Format),
                row.Discount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "DueDate",
                entity.DueDate.ToString(CS.DateOnly_Format_For_Validation),
                row.DueDate.ToString(CS.DateOnly_Format_For_Validation));

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

            _generalRepo.Compare(changes, logs, "OriginalCustomerId",
                StringHelper.NormalizeString(entity.OriginalCustomerId.ToString()),
                StringHelper.NormalizeString(row.OriginalCustomerId.ToString()));

            _generalRepo.Compare(changes, logs, "OriginalProductId",
                StringHelper.NormalizeString(entity.OriginalProductId.ToString()),
                StringHelper.NormalizeString(row.OriginalProductId.ToString()));

            _generalRepo.Compare(changes, logs, "OriginalSeriesNumber",
                StringHelper.NormalizeString(entity.OriginalSeriesNumber),
                StringHelper.NormalizeString(row.OriginalSeriesNumber));

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

        public async Task<FindSalesInvoiceInDbContextDto> BuildLookupSalesInvoiceContextForAasAsync(
            IEnumerable<SalesInvoiceUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalCustomerIds = rows.Select(r => r.OriginalCustomerId).Distinct().ToList();
            var originalProductIds = rows.Select(r => r.OriginalProductId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindSalesInvoiceInDbContextDto
            {
                ExistingInvoices = await _aasDbContext.SalesInvoices
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                CustomerId = await _aasDbContext.Customers
                    .Where(x => originalCustomerIds.Contains(x.OriginalCustomerId))
                    .GroupBy(x => x.OriginalCustomerId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalCustomerId, x => x.CustomerId, cancellationToken),

                ProductId = await _aasDbContext.Products
                    .Where(x => originalProductIds.Contains(x.OriginalProductId))
                    .GroupBy(x => x.OriginalProductId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalProductId, x => x.ProductId, cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public async Task SalesBookPosting(
            SalesInvoiceUploadExcelFileViewModel row,
            CancellationToken cancellationToken)
        {
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.OriginalCustomerId == row.OriginalCustomerId, cancellationToken);
            var product = await _dbContext.Products.FirstOrDefaultAsync(x => x.OriginalProductId == row.OriginalProductId, cancellationToken);

            var salesBook = new SalesBook
            {
                TransactionDate = row.TransactionDate,
                SerialNo = row.SalesInvoiceNo,
                SoldTo = customer!.CustomerName,
                TinNo = customer.CustomerTin,
                Address = customer.CustomerAddress,
                Description = product!.ProductName,
                Amount = row.Amount - row.Discount,
                Discount = row.Discount,
                CreatedBy = row.CreatedBy,
                CreatedDate = row.CreatedDate,
                DueDate = row.DueDate,
                VatableSales = customer.VatType == CS.VatType_Vatable ? _generalRepo.ComputeNetOfVat(row.Amount) : row.Amount,
                VatAmount = _generalRepo.ComputeVatAmount(customer.VatType == CS.VatType_Vatable ? _generalRepo.ComputeNetOfVat(row.Amount) : row.Amount),
                NetSales = customer.VatType == CS.VatType_Vatable ? _generalRepo.ComputeNetOfVat(row.Amount) : row.Amount - row.Discount,
            };

            await _dbContext.SalesBooks.AddAsync(salesBook, cancellationToken);
        }

        public async Task GeneralLedgerBookPosting(
            SalesInvoiceUploadExcelFileViewModel row,
            CancellationToken cancellationToken)
        {
            var customer = _dbContext.Customers.FirstOrDefault(x => x.OriginalCustomerId == row.OriginalCustomerId);
            var product = _dbContext.Products.FirstOrDefault(x => x.OriginalProductId == row.OriginalProductId);


            decimal netDiscount = row.Amount - row.Discount;
            decimal netOfVatAmount = customer.VatType == CS.VatType_Vatable ? _generalRepo.ComputeNetOfVat(netDiscount) : netDiscount;
            decimal vatAmount = customer.VatType == CS.VatType_Vatable ? _generalRepo.ComputeVatAmount(netOfVatAmount) : 0;
            decimal withHoldingTaxAmount = customer.WithHoldingTax ? _generalRepo.ComputeEwtAmount(netOfVatAmount, row.CwtPercent) : 0;
            decimal withHoldingVatAmount = customer.WithHoldingVat ? _generalRepo.ComputeEwtAmount(netOfVatAmount, row.CwVatPercent) : 0;

            var accountTitlesDto = await _generalRepo.GetListOfAccountTitleDto(cancellationToken);
            var arTradeReceivableTitle = accountTitlesDto.Find(c => c.AccountNumber == "101020100") ?? throw new ArgumentException("Account number: '101020100', Account title: 'AR-Trade Receivable' not found.");
            var arTradeCwt = accountTitlesDto.Find(c => c.AccountNumber == "101020200") ?? throw new ArgumentException("Account number: '101020200', Account title: 'AR-Trade Receivable - Creditable Withholding Tax' not found.");
            var arTradeCwv = accountTitlesDto.Find(c => c.AccountNumber == "101020300") ?? throw new ArgumentException("Account number: '101020300', Account title: 'AR-Trade Receivable - Creditable Withholding Vat' not found.");
            var (salesAcctNo, _) = _generalRepo.GetSalesAccountTitle(product.ProductCode!);
            var salesTitle = accountTitlesDto.Find(c => c.AccountNumber == salesAcctNo) ?? throw new ArgumentException($"Account title '{salesAcctNo}' not found.");
            var vatOutputTitle = accountTitlesDto.Find(c => c.AccountNumber == "201030100") ?? throw new ArgumentException("Account number: '201030100', Account title: 'Vat - Output' not found.");


            var ledgers = new List<GeneralLedgerBook>
            {
                new()
                {
                    Date = row.TransactionDate,
                    Reference = row.SalesInvoiceNo!,
                    Description = product.ProductName,
                    AccountNo = arTradeReceivableTitle.AccountNumber,
                    AccountTitle = arTradeReceivableTitle.AccountName,
                    Debit = netDiscount - (withHoldingTaxAmount + withHoldingVatAmount),
                    Credit = 0,
                    CreatedBy = row.CreatedBy,
                    CreatedDate = row.CreatedDate
                }
            };

            if (withHoldingTaxAmount > 0)
            {
                ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.SalesInvoiceNo!,
                        Description = product.ProductName,
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
                        Date = row.TransactionDate,
                        Reference = row.SalesInvoiceNo!,
                        Description = product.ProductName,
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
                    Date = row.TransactionDate,
                    Reference = row.SalesInvoiceNo!,
                    Description = product.ProductName,
                    AccountNo = salesTitle.AccountNumber,
                    AccountTitle = salesTitle.AccountName,
                    Debit = 0,
                    Credit = netOfVatAmount,
                    CreatedBy = row.CreatedBy,
                    CreatedDate = row.CreatedDate
                }
            );
            if (vatAmount > 0)
            {
                ledgers.Add(
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.SalesInvoiceNo!,
                        Description = product.ProductName,
                        AccountNo = vatOutputTitle.AccountNumber,
                        AccountTitle = vatOutputTitle.AccountName,
                        Debit = 0,
                        Credit = vatAmount,
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

        public async Task AddSalesToInventoryBookAsync(SalesInvoiceUploadExcelFileViewModel row, CancellationToken cancellationToken = default)
        {
            var product = _dbContext.Products.FirstOrDefault(x => x.OriginalProductId == row.OriginalProductId);

            var sortedInventory = await _dbContext.Inventories
            .Where(i => i.ProductId == product.ProductId)
            .OrderBy(i => i.Date)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

            var lastIndex = sortedInventory.FindLastIndex(s => s.Date <= row.TransactionDate);
            if (lastIndex >= 0)
            {
                sortedInventory = sortedInventory.Skip(lastIndex).ToList();
            }
            else
            {
                throw new ArgumentException($"Beginning inventory for {product.ProductName} not found!");
            }

            var previousInventory = sortedInventory.FirstOrDefault();

            if (previousInventory != null)
            {
                if (previousInventory.InventoryBalance < row.Quantity)
                {
                    throw new InvalidOperationException($"The quantity exceeds the available inventory of '{product.ProductName}'.");
                }

                decimal total = row.Quantity * previousInventory.AverageCost;
                decimal inventoryBalance = previousInventory.InventoryBalance - row.Quantity;
                decimal totalBalance = previousInventory.TotalBalance - total;
                decimal averageCost = inventoryBalance != 0 && totalBalance != 0 ? totalBalance / inventoryBalance : previousInventory.AverageCost;

                Inventory inventory = new()
                {
                    Date = row.TransactionDate,
                    ProductId = product.ProductId,
                    Particular = "Sales",
                    Reference = row.SalesInvoiceNo,
                    Quantity = row.Quantity,
                    Cost = previousInventory.AverageCost,
                    IsValidated = true,
                    ValidatedBy = row.PostedBy,
                    ValidatedDate = DateTime.Now,
                    Total = total,
                    InventoryBalance = inventoryBalance,
                    TotalBalance = totalBalance,
                    AverageCost = averageCost
                };


                foreach (var transaction in sortedInventory.Skip(1))
                {
                    if (transaction.Particular == "Sales")
                    {
                        transaction.Cost = averageCost;
                        transaction.Total = transaction.Quantity * averageCost;
                        transaction.TotalBalance = totalBalance != 0 ? totalBalance - transaction.Total : transaction.Total;
                        transaction.InventoryBalance = inventoryBalance != 0 ? inventoryBalance - transaction.Quantity : transaction.Quantity;
                        transaction.AverageCost = transaction.TotalBalance != 0 && transaction.InventoryBalance != 0 ? transaction.TotalBalance / transaction.InventoryBalance : averageCost;
                        var costOfGoodsSold = transaction.AverageCost * transaction.Quantity;

                        averageCost = transaction.AverageCost;
                        totalBalance = transaction.TotalBalance;
                        inventoryBalance = transaction.InventoryBalance;

                        var journalEntries = await _dbContext.GeneralLedgerBooks
                            .Where(j => j.Reference == transaction.Reference &&
                                        (j.AccountNo.StartsWith("50101") || j.AccountNo.StartsWith("10104")))
                            .ToListAsync(cancellationToken);

                        if (journalEntries.Count != 0)
                        {
                            foreach (var journal in journalEntries)
                            {
                                if (journal.Debit != 0)
                                {
                                    if (journal.Debit != costOfGoodsSold)
                                    {
                                        journal.Debit = costOfGoodsSold;
                                        journal.Credit = 0;
                                    }
                                }
                                else
                                {
                                    if (journal.Credit != costOfGoodsSold)
                                    {
                                        journal.Credit = costOfGoodsSold;
                                        journal.Debit = 0;
                                    }
                                }
                            }
                        }

                        _dbContext.GeneralLedgerBooks.UpdateRange(journalEntries);

                    }
                    else if (transaction.Particular == "Purchases")
                    {
                        transaction.TotalBalance = totalBalance + transaction.Total;
                        transaction.InventoryBalance = inventoryBalance + transaction.Quantity;
                        transaction.AverageCost = transaction.TotalBalance != 0 && transaction.InventoryBalance != 0 ? transaction.TotalBalance / transaction.InventoryBalance : averageCost;

                        averageCost = transaction.AverageCost;
                        totalBalance = transaction.TotalBalance;
                        inventoryBalance = transaction.InventoryBalance;
                    }
                }

                var ledgers = new List<GeneralLedgerBook>
                {
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.SalesInvoiceNo!,
                        Description = product.ProductName,
                        AccountNo = product.ProductCode == "PET001" ? "501010100" : product.ProductCode == "PET002" ? "501010200" : "501010300",
                        AccountTitle = product.ProductCode == "PET001" ? "COGS - Biodiesel" : product.ProductCode == "PET002" ? "COGS - Econogas" : "COGS - Envirogas",
                        Debit = inventory.Total,
                        Credit = 0,
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    },
                    new GeneralLedgerBook
                    {
                        Date = row.TransactionDate,
                        Reference = row.SalesInvoiceNo!,
                        Description = product.ProductName,
                        AccountNo = product.ProductCode == "PET001" ? "101040100" : product.ProductCode == "PET002" ? "101040200" : "101040300",
                        AccountTitle = product.ProductCode == "PET001" ? "Inventory - Biodiesel" : product.ProductCode == "PET002" ? "Inventory - Econogas" : "Inventory - Envirogas",
                        Debit = 0,
                        Credit = inventory.Total,
                        CreatedBy = row.CreatedBy,
                        CreatedDate = row.CreatedDate
                    }
                };

                _dbContext.Inventories.UpdateRange(sortedInventory);

                if (_generalRepo.IsJournalEntriesBalanced(ledgers))
                {
                    await _dbContext.Inventories.AddAsync(inventory, cancellationToken);
                    await _dbContext.GeneralLedgerBooks.AddRangeAsync(ledgers, cancellationToken);
                }
                else
                {
                    throw new ArgumentException("Debit and Credit is not equal, check your entries.");
                }


            }
            else
            {
                throw new InvalidOperationException($"Beginning inventory for this product '{product.ProductName}' not found!");
            }
        }
    }
}
