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
    public class PurchaseOrderRepo
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly GeneralRepo _generalRepo;

        private readonly AasDbContext _aasDbContext;

        public PurchaseOrderRepo(ApplicationDbContext dbContext, GeneralRepo generalRepo, AasDbContext aasDbContext)
        {
            _dbContext = dbContext;
            _generalRepo = generalRepo;
            _aasDbContext = aasDbContext;
        }

        public async Task<List<PurchaseOrder>> GetPurchaseOrderAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext
                .PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Product)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GeneratePONo(CancellationToken cancellationToken = default)
        {
            var purchaseOrder = await _dbContext
                .PurchaseOrders
                .Where(po => !po.PurchaseOrderNo!.StartsWith("POBEG"))
                .OrderByDescending(s => s.PurchaseOrderNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (purchaseOrder != null)
            {
                string lastSeries = purchaseOrder.PurchaseOrderNo ?? throw new InvalidOperationException("PONo is null pls Contact MIS Enterprise");
                string numericPart = lastSeries.Substring(2);
                int incrementedNumber = int.Parse(numericPart) + 1;

                return lastSeries.Substring(0,2) + incrementedNumber.ToString("D10");
            }
            else
            {
                return $"PO{1.ToString("D10")}";
            }
        }

        public async Task<int> GetSupplierNoAsync(int? id, CancellationToken cancellationToken = default)
        {
            if (id != 0)
            {
                var supplier = await _dbContext
                                .Suppliers
                                .FirstOrDefaultAsync(s => s.SupplierId == id, cancellationToken);
                return supplier!.Number;
            }
            else
            {
                throw new ArgumentException("No record found in supplier.");
            }
        }

        public async Task<string> GetProductNoAsync(int? id, CancellationToken cancellationToken = default)
        {
            if (id != 0)
            {
                var product = await _dbContext
                                .Products
                                .FirstOrDefaultAsync(s => s.ProductId == id, cancellationToken);
                return product!.ProductCode!;
            }

            throw new ArgumentException("No record found in supplier.");
        }

        public async Task<PurchaseOrder> FindPurchaseOrder(int? id, CancellationToken cancellationToken = default)
        {
            var po = await _dbContext
                .PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Product)
                .FirstOrDefaultAsync(po => po.PurchaseOrderId == id, cancellationToken);

            if (po != null)
            {
                return po;
            }
            else
            {
                throw new ArgumentException("Invalid id value. The id must be greater than 0.");
            }
        }

        public async Task LogChangesAsync(int id, Dictionary<string, (string OriginalValue, string NewValue)> changes, string? modifiedBy, string seriesNumber, string databaseName)
        {
            foreach (var change in changes)
            {
                var logReport = new ImportExportLog()
                {
                    Id = Guid.NewGuid(),
                    TableName = nameof(DynamicView.PurchaseOrder),
                    DocumentRecordId = id,
                    ColumnName = change.Key,
                    Module = "Purchase Order",
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

        public IReadOnlyList<PurchaseOrderUploadExcelFileViewModel> ParseWorksheet(ExcelWorksheet worksheet)
        {
            var sheet = new ImportWorksheet(worksheet);
            var rows = new List<PurchaseOrderUploadExcelFileViewModel>();
            for (var row = 2; row <= worksheet.Dimension.Rows; row++)
            {
                if (sheet.IsEmpty(row)) continue;
                rows.Add(new PurchaseOrderUploadExcelFileViewModel
                {
                    PurchaseOrderNo = sheet.Text(row, "OriginalPONo", "OriginalSeriesNumber", "PurchaseOrderNo", "OriginalJVNo"),
                    OriginalSeriesNumber = sheet.Text(row, "OriginalPONo", "OriginalSeriesNumber", "PurchaseOrderNo", "OriginalJVNo"),
                    Date = DateOnly.FromDateTime(sheet.Date(row, "Date", true)),
                    Terms = sheet.Text(row, "Terms"),
                    Quantity = sheet.Value<decimal>(row, "Quantity"),
                    Price = sheet.Value<decimal>(row, "Price"),
                    Amount = sheet.Value<decimal>(row, "Amount"),
                    FinalPrice = string.IsNullOrWhiteSpace(sheet.Text(row, "FinalPrice")) ? null : sheet.Value<decimal>(row, "FinalPrice"),
                    Remarks = sheet.Text(row, "Remarks"),
                    CreatedBy = sheet.Text(row, "CreatedBy"),
                    CreatedDate = sheet.Date(row, "CreatedDate", true),
                    PostedBy = sheet.Text(row, "PostedBy"),
                    PostedDate = sheet.Date(row, "PostedDate"),
                    IsClosed = sheet.Value<bool>(row, "IsClosed"),
                    CancellationRemarks = sheet.Text(row, "CancellationRemarks"),
                    OriginalProductId = sheet.Value<int>(row, "OriginalProductId"),
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

        public async Task<FindPurchaseOrderInDbContextDto> BuildLookupPurchaseOrderContextAsync(
            IEnumerable<PurchaseOrderUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalProductIds = rows.Select(r => r.OriginalProductId).Distinct().ToList();
            var originalSupplierIds = rows.Select(r => r.OriginalSupplierId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindPurchaseOrderInDbContextDto
            {
                ExistingPurchaseOrder = await _dbContext.PurchaseOrders
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                ExistingProduct = await _dbContext.Products
                    .Where(x => originalProductIds.Contains(x.OriginalProductId))
                    .GroupBy(x => x.OriginalProductId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(
                        x => x.OriginalProductId,
                        x => (x.ProductId, x.ProductCode),
                        cancellationToken),

                ExistingSuppliers = await _dbContext.Suppliers
                    .Where(x => originalSupplierIds.Contains(x.OriginalSupplierId!.Value))
                    .GroupBy(x => x.OriginalSupplierId!.Value)
                    .Select(g => g.First())
                    .ToDictionaryAsync(
                        x => x.OriginalSupplierId!.Value,
                        x => (x.SupplierId, x.Number),
                        cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }

        public PurchaseOrder MapToPurchaseOrderEntity(
            PurchaseOrderUploadExcelFileViewModel row,
            FindPurchaseOrderInDbContextDto context)
        {
            if (!context.ExistingProduct.TryGetValue(row.OriginalProductId, out var productData))
            {
                throw new InvalidOperationException($"Product id missing for PO#{row.PurchaseOrderNo}.");
            }

            if (!context.ExistingSuppliers.TryGetValue(row.OriginalSupplierId, out var supplierData))
            {
                throw new InvalidOperationException($"Supplier id missing for PO#{row.PurchaseOrderNo}.");
            }

            return new PurchaseOrder
            {
                PurchaseOrderNo = row.PurchaseOrderNo,
                Date = row.Date,
                Terms = row.Terms,
                Quantity = row.Quantity,
                Price = row.Price,
                Amount = row.Amount,
                FinalPrice = row.FinalPrice,
                Remarks = row.Remarks,
                CreatedBy = row.CreatedBy,
                CreatedDate = row.CreatedDate,
                IsPosted = !string.IsNullOrEmpty(row.PostedBy),
                PostedBy = row.PostedBy,
                PostedDate = row.PostedDate,
                IsClosed = row.IsClosed,
                CancellationRemarks = row.CancellationRemarks,
                IsCanceled = !string.IsNullOrEmpty(row.CanceledBy),
                CanceledBy = row.CanceledBy,
                CanceledDate = row.CanceledDate,
                IsVoided = !string.IsNullOrEmpty(row.VoidedBy),
                VoidedBy = row.VoidedBy,
                VoidedDate = row.VoidedDate,
                EditedBy = row.EditedBy,
                EditedDate = row.EditedDate,
                OriginalProductId = row.OriginalProductId,
                OriginalSeriesNumber = row.OriginalSeriesNumber,
                OriginalSupplierId = row.OriginalSupplierId,
                OriginalDocumentId = row.OriginalDocumentId,

                ProductId = productData.ProductId,
                ProductNo = productData.ProductCode,
                SupplierId = supplierData.SupplierId,
                SupplierNo = supplierData.SupplierNo
            };
        }

        public IEnumerable<AuditTrail> AuditTrails(
            PurchaseOrderUploadExcelFileViewModel row,
            string machineName)
        {
            var audits = new List<AuditTrail>();

            if (!string.IsNullOrWhiteSpace(row.CreatedBy))
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CreatedBy,
                    Activity = $"Create new purchase order# {row.PurchaseOrderNo}",
                    DocumentType = "Purchase Order",
                    MachineName = machineName,
                    Date = row.CreatedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.PostedBy) && row.PostedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.PostedBy,
                    Activity = $"Posted purchase order# {row.PurchaseOrderNo}",
                    DocumentType = "Purchase Order",
                    MachineName = machineName,
                    Date = row.PostedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.CanceledBy) && row.CanceledDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.CanceledBy,
                    Activity = $"Cancelled purchase order# {row.PurchaseOrderNo}",
                    DocumentType = "Purchase Order",
                    MachineName = machineName,
                    Date = row.CanceledDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.VoidedBy) && row.VoidedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.VoidedBy,
                    Activity = $"Voided purchase order# {row.PurchaseOrderNo}",
                    DocumentType = "Purchase Order",
                    MachineName = machineName,
                    Date = row.VoidedDate
                });
            }

            if (!string.IsNullOrWhiteSpace(row.EditedBy) && row.EditedDate != default)
            {
                audits.Add(new AuditTrail
                {
                    Username = row.EditedBy,
                    Activity = $"Edited purchase order# {row.PurchaseOrderNo}",
                    DocumentType = "Purchase Order",
                    MachineName = machineName,
                    Date = row.EditedDate
                });
            }

            return audits;
        }

        public Dictionary<string, (string Original, string New)> Detect(
            PurchaseOrder entity,
            PurchaseOrderUploadExcelFileViewModel row,
            IReadOnlyList<ImportExportLog> logs)
        {
            var changes = new Dictionary<string, (string, string)>();

            _generalRepo.Compare(changes, logs, "PurchaseOrderNo",
                StringHelper.NormalizeString(entity.PurchaseOrderNo),
                row.PurchaseOrderNo);

            _generalRepo.Compare(changes, logs, "Date",
                entity.Date.ToString(CS.DateOnly_Format_For_Validation),
                row.Date.ToString(CS.DateOnly_Format_For_Validation));

            _generalRepo.Compare(changes, logs, "Terms",
                StringHelper.NormalizeString(entity.Terms),
                row.Terms);

            _generalRepo.Compare(changes, logs, "Quantity",
                entity.Quantity.ToString(CS.Four_Decimal_Format),
                row.Quantity.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Price",
                entity.Price.ToString(CS.Four_Decimal_Format),
                row.Price.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "Amount",
                entity.Amount.ToString(CS.Four_Decimal_Format),
                row.Amount.ToString(CS.Four_Decimal_Format));

            _generalRepo.Compare(changes, logs, "FinalPrice",
                entity.FinalPrice?.ToString(CS.Four_Decimal_Format) ?? String.Empty,
                row.FinalPrice?.ToString(CS.Four_Decimal_Format) ?? string.Empty);

            _generalRepo.Compare(changes, logs, "Remarks",
                StringHelper.NormalizeString(entity.Remarks),
                row.Remarks);

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
                row.PostedDate.ToString(CS.ImportDateTimeFormat)
            );

            _generalRepo.Compare(changes, logs, "IsClosed",
                entity.IsClosed.ToString(),
                row.IsClosed.ToString());

            _generalRepo.Compare(changes, logs, "CancellationRemarks",
                StringHelper.NormalizeString(entity.CancellationRemarks),
                row.CancellationRemarks);

            _generalRepo.Compare(changes, logs, "OriginalProductId",
                entity.OriginalProductId?.ToString() ?? "0",
                row.OriginalProductId.ToString());

            _generalRepo.Compare(changes, logs, "OriginalSeriesNumber",
                StringHelper.NormalizeString(entity.OriginalSeriesNumber),
                row.OriginalSeriesNumber);

            _generalRepo.Compare(changes, logs, "OriginalSupplierId",
                entity.OriginalSupplierId?.ToString() ?? "0",
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

            return changes;
        }

        public async Task<FindPurchaseOrderInDbContextDto> BuildLookupPurchaseOrderContextForAasAsync(
            IEnumerable<PurchaseOrderUploadExcelFileViewModel> rows,
            CancellationToken cancellationToken)
        {
            var originalProductIds = rows.Select(r => r.OriginalProductId).Distinct().ToList();
            var originalSupplierIds = rows.Select(r => r.OriginalSupplierId).Distinct().ToList();
            var originalSeriesNumbers = rows.Select(r => r.OriginalSeriesNumber).Distinct().ToList();

            return new FindPurchaseOrderInDbContextDto
            {
                ExistingPurchaseOrder = await _aasDbContext.PurchaseOrders
                    .Where(x => originalSeriesNumbers.Contains(x.OriginalSeriesNumber))
                    .GroupBy(x => x.OriginalSeriesNumber)
                    .Select(x => x.First())
                    .ToDictionaryAsync(x => x.OriginalSeriesNumber, cancellationToken),

                ExistingProduct = await _aasDbContext.Products
                    .Where(x => originalProductIds.Contains(x.OriginalProductId))
                    .GroupBy(x => x.OriginalProductId)
                    .Select(x => x.First())
                    .ToDictionaryAsync(
                        x => x.OriginalProductId,
                        x => (x.ProductId, x.ProductCode),
                        cancellationToken),

                ExistingSuppliers = await _aasDbContext.Suppliers
                    .Where(x => originalSupplierIds.Contains(x.OriginalSupplierId!.Value))
                    .GroupBy(x => x.OriginalSupplierId!.Value)
                    .Select(g => g.First())
                    .ToDictionaryAsync(
                        x => x.OriginalSupplierId!.Value,
                        x => (x.SupplierId, x.Number),
                        cancellationToken),

                ExistingLogs = await _dbContext.ImportExportLogs
                    .Where(x => originalSeriesNumbers.Contains(x.DocumentNo))
                    .ToListAsync(cancellationToken)
            };
        }
    }
}
