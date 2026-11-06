using System.Drawing;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Repository;
using Accounting_System.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Accounting_System.Controllers
{
    public class ImportExportLogsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly AasDbContext _aasDbContext;

        private readonly GeneralRepo _generalRepo;

        public ImportExportLogsController(ApplicationDbContext dbContext, AasDbContext aasDbContext, GeneralRepo generalRepo)
        {
            _dbContext = dbContext;
            _aasDbContext = aasDbContext;
            _generalRepo = generalRepo;
        }
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var importExportLogs = await _dbContext.ImportExportLogs
                .Where(iel => iel.Action == string.Empty).ToListAsync(cancellationToken);

            return View(importExportLogs);
        }

        public async Task<IActionResult> ImportAction(Guid id, string procedure, string tableName, string columnName, CancellationToken cancellationToken)
        {
            var importRecord = await _dbContext.ImportExportLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (importRecord == null)
            {
                return NotFound();
            }

            if (importRecord.DatabaseName == "IBS-RCD")
                return await ImportIbsActionAsync(importRecord, procedure, cancellationToken);

            if (importRecord.DatabaseName == "AAS")
                return await ImportAasActionAsync(importRecord, procedure, cancellationToken);
            return BadRequest("Unsupported import database.");
        }
        private async Task<IActionResult> ImportAasActionAsync(ImportExportLog record, string procedure, CancellationToken token)
        {
            if (procedure is not ("Modify" or "Keep")) return BadRequest("Choose Modify or Keep.");
            await using var reviewTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
            try
            {
                await _dbContext.Entry(record).ReloadAsync(token);
                if (_dbContext.Entry(record).State == EntityState.Detached) return NotFound();
                if (record.DatabaseName != "AAS") return Conflict("The log target changed. Reload the log list.");
                if (record.Action != string.Empty || record.Executed) return Conflict("This change has already been reviewed.");
                var alreadyApplied = await _aasDbContext.ImportExportLogs.AnyAsync(x => x.Id == record.Id &&
                    (x.Action != string.Empty || x.Executed), token);
                var group = await IbsImportLogActions.ReviewGroupAsync(_dbContext, record, procedure == "Modify" && !alreadyApplied, token);
                await using (var targetTransaction = await _aasDbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token))
                {
                    var ids = group.Select(x => x.Id).ToList();
                    var receipts = await _aasDbContext.ImportExportLogs.Where(x => ids.Contains(x.Id)).ToListAsync(token);
                    var completed = receipts.Where(x => x.Action != string.Empty || x.Executed).ToList();
                    if (completed.Any(x => x.Action != procedure) || completed.Count != 0 && completed.Count != group.Count)
                        return Conflict("This proposal has already been applied in AAS. Complete the same review action.");
                    if (completed.Count == 0)
                    {
                        if (procedure == "Modify") await IbsImportLogActions.ApplyAsync(_aasDbContext, record, group, token);
                        foreach (var item in group)
                        {
                            var receipt = receipts.SingleOrDefault(x => x.Id == item.Id);
                            if (receipt == null)
                            {
                                receipt = new ImportExportLog();
                                _aasDbContext.Entry(receipt).CurrentValues.SetValues(item);
                                _aasDbContext.ImportExportLogs.Add(receipt);
                            }
                            receipt.Action = procedure;
                            receipt.Executed = procedure == "Modify";
                        }
                        await _aasDbContext.SaveChangesAsync(token);
                    }
                    await targetTransaction.CommitAsync(token);
                }
                foreach (var item in group)
                {
                    item.Action = procedure;
                    item.Executed = procedure == "Modify";
                }
                await _dbContext.SaveChangesAsync(token);
                await reviewTransaction.CommitAsync(token);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException or ArgumentException or DbUpdateException)
            {
                await reviewTransaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                _aasDbContext.ChangeTracker.Clear();
                return BadRequest(ex.Message);
            }
            catch
            {
                await reviewTransaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                _aasDbContext.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task<IActionResult> ImportIbsActionAsync(ImportExportLog record, string procedure, CancellationToken token)
        {
            if (procedure is not ("Modify" or "Keep")) return BadRequest("Choose Modify or Keep.");
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
            try
            {
                await _dbContext.Entry(record).ReloadAsync(token);
                if (_dbContext.Entry(record).State == EntityState.Detached) return NotFound();
                if (record.DatabaseName != "IBS-RCD") return Conflict("The log target changed. Reload the log list.");
                if (record.Action != string.Empty || record.Executed) return Conflict("This change has already been reviewed.");
                var group = await IbsImportLogActions.ReviewGroupAsync(_dbContext, record, procedure == "Modify", token);
                if (procedure == "Modify") await IbsImportLogActions.ApplyAsync(_dbContext, record, group, token);
                foreach (var item in group)
                {
                    item.Action = procedure;
                    item.Executed = procedure == "Modify";
                }
                await _dbContext.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException or ArgumentException or DbUpdateException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return BadRequest(ex.Message);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        #region -- save as excel sales invoice report --

        public async Task<IActionResult> GenerateLogReport(CancellationToken cancellationToken)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
		    {
                var extractedBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                // Retrieve the selected invoices from the database
                var importExportLogList = await _dbContext.ImportExportLogs
                    .OrderBy(invoice => invoice.TimeStamp)
                    .ToListAsync(cancellationToken: cancellationToken);

                // Create the Excel package
                using var package = new ExcelPackage();
                // Add a new worksheet to the Excel package
                var worksheet = package.Workbook.Worksheets.Add("Import/Export Log");

                var mergedCells = worksheet.Cells["A1:C1"];
                mergedCells.Merge = true;
                    mergedCells.Value = "IMPORT/EXPORT LOG REPORT";
                mergedCells.Style.Font.Size = 13;

                worksheet.Cells["A2"].Value = "Extracted By:";
                worksheet.Cells["A3"].Value = "Company:";

                worksheet.Cells["B2"].Value = $"{extractedBy}";
                worksheet.Cells["B3"].Value = "FILPRIDE";

                worksheet.Cells["A7"].Value = "DOCUMENT RECORD ID";
                worksheet.Cells["B7"].Value = "SERIES NUMBER";
                worksheet.Cells["C7"].Value = "TABLE NAME";
                worksheet.Cells["D7"].Value = "COLUMN NAME";
                worksheet.Cells["E7"].Value = "ORIGINAL VALUE";
                worksheet.Cells["F7"].Value = "ADJUSTED VALUE";
                worksheet.Cells["G7"].Value = "TIME STAMP";
                worksheet.Cells["H7"].Value = "UPLOADED BY";
                worksheet.Cells["I7"].Value = "STATUS";

                // Apply styling to the header row
                using (var range = worksheet.Cells["A7:I7"])
                {
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                int row = 8;

                foreach (var item in importExportLogList)
                {
                    worksheet.Cells[row, 1].Value = item.DocumentRecordId;
                    worksheet.Cells[row, 2].Value = item.DocumentNo;
                    worksheet.Cells[row, 3].Value = item.TableName;
                    worksheet.Cells[row, 4].Value = item.ColumnName;
                    worksheet.Cells[row, 5].Value = item.OriginalValue;
                    worksheet.Cells[row, 6].Value = item.AdjustedValue;
                    worksheet.Cells[row, 7].Value = item.TimeStamp.ToString("MM/dd/yyyy HH:mm:ss tt");
                    worksheet.Cells[row, 8].Value = item.UploadedBy;
                    worksheet.Cells[row, 9].Value = item.Action;

                    row++;
                }

                worksheet.Cells.AutoFitColumns();
                worksheet.View.FreezePanes(8, 1);

                // Convert the Excel package to a byte array
                var excelBytes = await package.GetAsByteArrayAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ImportExportLogReport_IBS-RCD_{DateTime.Now:yyyyddMMHHmmss}.xlsx");
		    }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(Index), new { view = DynamicView.BankAccount });
            }

        }

        #endregion
    }
}
