using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.Reports;
using Accounting_System.Repository;
using Accounting_System.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Controllers
{
    [Authorize]
    public class ServiceController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly AasDbContext _aasDbContext;

        private readonly ServiceRepo _serviceRepo;

        private readonly GeneralRepo _generalRepo;

        public ServiceController(ApplicationDbContext dbContext, ServiceRepo serviceRepo, AasDbContext aasDbContext, GeneralRepo generalRepo)
        {
            _dbContext = dbContext;
            _serviceRepo = serviceRepo;
            _aasDbContext = aasDbContext;
            _generalRepo = generalRepo;

        }

        public async Task<IActionResult> Index(string? view, CancellationToken cancellationToken)
        {
            var data = await _dbContext.Services.ToListAsync(cancellationToken);

            if (view == nameof(DynamicView.Service))
            {
                return View("ImportExportIndex");
            }

            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllServiceIds(CancellationToken cancellationToken)
        {
            var serviceIds = await _dbContext.Services
                                     .Select(s => s.ServiceId) // Assuming Id is the primary key
                                     .ToListAsync(cancellationToken);
            return Json(serviceIds);
        }

        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var viewModel = new Services
            {
                CurrentAndPreviousTitles = await _dbContext.ChartOfAccounts.Where(x => !x.IsHidden)
                    .Where(coa => !coa.HasChildren && !coa.IsHidden)
                    .OrderBy(coa => coa.AccountId)
                    .Select(s => new SelectListItem
                    {
                        Value = s.AccountId.ToString(),
                        Text = s.AccountNumber + " " + s.AccountName
                    })
                    .ToListAsync(cancellationToken),
                UnearnedTitles = await _dbContext.ChartOfAccounts.Where(x => !x.IsHidden)
                    .Where(coa => !coa.HasChildren && !coa.IsHidden)
                    .OrderBy(coa => coa.AccountId)
                    .Select(s => new SelectListItem
                    {
                        Value = s.AccountId.ToString(),
                        Text = s.AccountNumber + " " + s.AccountName
                    })
                    .ToListAsync(cancellationToken)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Services services, CancellationToken cancellationToken)
        {
            services.CurrentAndPreviousTitles = await _dbContext.ChartOfAccounts.Where(x => !x.IsHidden)
                .Where(coa => !coa.HasChildren && !coa.IsHidden)
                .OrderBy(coa => coa.AccountId)
                .Select(s => new SelectListItem
                {
                    Value = s.AccountId.ToString(),
                    Text = s.AccountNumber + " " + s.AccountName
                })
                .ToListAsync(cancellationToken);

            services.UnearnedTitles = await _dbContext.ChartOfAccounts.Where(x => !x.IsHidden)
                .Where(coa => !coa.HasChildren && !coa.IsHidden)
                .OrderBy(coa => coa.AccountId)
                .Select(s => new SelectListItem
                {
                    Value = s.AccountId.ToString(),
                    Text = s.AccountNumber + " " + s.AccountName
                })
                .ToListAsync(cancellationToken);

            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                try
                {
                    if (await _serviceRepo.IsServicesExist(services.Name, cancellationToken))
                    {
                        ModelState.AddModelError("Name", "Services already exist!");
                        return View(services);
                    }

                    if (services.Percent == 0)
                    {
                        ModelState.AddModelError("Percent", "Please input percent!");
                        return View(services);
                    }

                    var currentAndPrevious = await _dbContext.ChartOfAccounts.Where(x => !x.IsHidden)
                        .FirstOrDefaultAsync(x => x.AccountId == services.CurrentAndPreviousId, cancellationToken);

                    var unearned = await _dbContext.ChartOfAccounts.Where(x => !x.IsHidden)
                        .FirstOrDefaultAsync(x => x.AccountId == services.UnearnedId, cancellationToken);

                    services.CurrentAndPreviousNo = currentAndPrevious!.AccountNumber;
                    services.CurrentAndPreviousTitle = currentAndPrevious.AccountName;

                    services.UnearnedNo = unearned!.AccountNumber;
                    services.UnearnedTitle = unearned.AccountName;

                    services.CreatedBy = createdBy;
                    services.ServiceNo = await _serviceRepo.GetLastNumber(cancellationToken);

                    #region --Audit Trail Recording

                    if (services.OriginalServiceId == 0)
                    {
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        AuditTrail auditTrailBook = new(createdBy, $"Created new service {services.Name}", "Service", ipAddress!);
                        await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                    }

                    #endregion --Audit Trail Recording

                    await _dbContext.AddAsync(services, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    TempData["success"] = "Services created successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                 await transaction.RollbackAsync(cancellationToken);
                 TempData["error"] = ex.Message;
                 return RedirectToAction(nameof(Index));
                }
            }
            return View(services);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
        {
            if (id == null || !_dbContext.Services.Any())
            {
                return NotFound();
            }

            var services = await _dbContext.Services.FirstOrDefaultAsync(x => x.ServiceId == id, cancellationToken);
            if (services == null)
            {
                return NotFound();
            }
            return View(services);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Services services, CancellationToken cancellationToken)
        {
            if (id != services.ServiceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (services.Percent == 0)
                {
                    ModelState.AddModelError("Percent", "Please input percent!");
                    return View(services);
                }
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                try
                {

                    var existingServices = await _dbContext.Services.FirstOrDefaultAsync(x => x.ServiceId == services.ServiceId, cancellationToken);
                    existingServices!.Name = services.Name;
                    existingServices.Percent = services.Percent;

                    if (_dbContext.ChangeTracker.HasChanges())
                    {
                        #region --Audit Trail Recording

                        if (services.OriginalServiceId == 0)
                        {
                            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                            AuditTrail auditTrailBook = new(createdBy, $"Update service {services.Name}", "Service", ipAddress!);
                            await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                        }

                        #endregion --Audit Trail Recording

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        TempData["success"] = "Services updated successfully";
                    }
                    else
                    {
                        throw new InvalidOperationException("No data changes!");
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (!ServicesExists(services.ServiceId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    TempData["error"] = ex.Message;
                    return View(services);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(services);
        }

        private bool ServicesExists(int id)
        {
            return _dbContext.Services != null! && _dbContext.Services.Any(e => e.ServiceId == id);
        }

        [HttpPost]
        public async Task<IActionResult> GetServiceList(CancellationToken cancellationToken)
        {
            try
            {
                var services = await _dbContext.Services.ToListAsync(cancellationToken);

                return Json(new
                {
                    data = services
                });
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        //Download as .xlsx file.(Export)
        #region -- export xlsx record --

        [HttpPost]
        public async Task<IActionResult> Export(string selectedRecord, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(selectedRecord))
            {
                // Handle the case where no invoices are selected
                return RedirectToAction(nameof(Index));
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
		    {
                var recordIds = selectedRecord.Split(',').Select(int.Parse).ToList();

                // Retrieve the selected invoices from the database
                var selectedList = await _dbContext.Services
                    .Where(service => recordIds.Contains(service.ServiceId))
                    .OrderBy(service => service.ServiceId)
                    .ToListAsync(cancellationToken: cancellationToken);

                // Create the Excel package
                using var package = new ExcelPackage();
                // Add a new worksheet to the Excel package
                var worksheet = package.Workbook.Worksheets.Add("Services");

                worksheet.Cells["A1"].Value = "CurrentAndPreviousTitle";
                worksheet.Cells["B1"].Value = "UneranedTitle";
                worksheet.Cells["C1"].Value = "Name";
                worksheet.Cells["D1"].Value = "Percent";
                worksheet.Cells["E1"].Value = "CreatedBy";
                worksheet.Cells["F1"].Value = "CreatedDate";
                worksheet.Cells["G1"].Value = "CurrentAndPreviousNo";
                worksheet.Cells["H1"].Value = "UnearnedNo";
                worksheet.Cells["I1"].Value = "OriginalServiceId";

                int row = 2;

                foreach (var item in selectedList)
                {
                    worksheet.Cells[row, 1].Value = item.CurrentAndPreviousTitle;
                    worksheet.Cells[row, 2].Value = item.UnearnedTitle;
                    worksheet.Cells[row, 3].Value = item.Name;
                    worksheet.Cells[row, 4].Value = item.Percent;
                    worksheet.Cells[row, 5].Value = item.CreatedBy;
                    worksheet.Cells[row, 6].Value = item.CreatedDate.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 7].Value = item.CurrentAndPreviousNo;
                    worksheet.Cells[row, 8].Value = item.UnearnedNo;
                    worksheet.Cells[row, 9].Value = item.ServiceId;

                    row++;
                }

                // Convert the Excel package to a byte array
                var excelBytes = await package.GetAsByteArrayAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ServiceList_IBS-RCD_{DateTime.Now:yyyyddMMHHmmss}.xlsx");
		    }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(Index), new { view = DynamicView.BankAccount });
            }
        }

        #endregion -- export xlsx record --

        //Upload as .xlsx file.(Import)
        #region -- import xlsx record from IBS --

        [HttpPost]
        public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
        {
            return await WorkbookImport.RunAsync(this, file, "Services", cancellationToken);
        }

        #endregion

        //Upload as .xlsx file.(Import)
        #region -- import xlsx record to AAS --

        [HttpPost]
        public Task<IActionResult> AasImport(IFormFile file, CancellationToken cancellationToken)
            => WorkbookImport.RunAsync(this, file, "Services", cancellationToken, aas: true);

        #endregion
    }
}
