using Accounting_System.Data;
using Accounting_System.Models.MasterFile;
using Accounting_System.Models.Reports;
using Accounting_System.Repository;
using Accounting_System.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Controllers
{
    [Authorize]
    public class BankAccountController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly AasDbContext _aasDbContext;

        private readonly UserManager<IdentityUser> _userManager;

        private readonly BankAccountRepo _bankAccountRepo;

        private readonly GeneralRepo _generalRepo;

        public BankAccountController(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager,
            BankAccountRepo bankAccountRepo, AasDbContext aasDbContext, GeneralRepo generalRepo)
        {
            _dbContext = dbContext;
            this._userManager = userManager;
            _bankAccountRepo = bankAccountRepo;
            _aasDbContext = aasDbContext;
            _generalRepo = generalRepo;
        }

        public async Task<IActionResult> Index(string? view, CancellationToken cancellationToken)
        {
            var ba = await _bankAccountRepo.GetBankAccountAsync(cancellationToken);

            if (view == nameof(DynamicView.BankAccount))
            {
                return View("ImportExportIndex");
            }

            return View(ba);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBankAccountIds(CancellationToken cancellationToken)
        {
            var bankAccountIds = await _dbContext.BankAccounts
                .Select(ba => ba.BankAccountId) // Assuming Id is the primary key
                .ToListAsync(cancellationToken);
            return Json(bankAccountIds);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(BankAccount model, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);

                try
                {
                    if (await _bankAccountRepo.IsBankAccountNameExist(model.AccountName, cancellationToken))
                    {
                        ModelState.AddModelError("AccountName", "Bank account name already exist!");
                        return View(model);
                    }

                    model.CreatedBy = createdBy;

                    #region --Audit Trail Recording

                    if (model.OriginalBankId == 0)
                    {
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        AuditTrail auditTrailBook = new(createdBy, $"Created new bank {model.AccountName}",
                            "Bank Account", ipAddress!);
                        await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                    }

                    #endregion --Audit Trail Recording

                    await _dbContext.AddAsync(model, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    TempData["success"] = "Bank created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                 await transaction.RollbackAsync(cancellationToken);
                 TempData["error"] = ex.Message;
                 return RedirectToAction(nameof(Index), new { view = DynamicView.ServiceInvoice });
                }
            }
            else
            {
                ModelState.AddModelError("", "The information you submitted is not valid!");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var existingModel = await _bankAccountRepo.FindBankAccount(id, cancellationToken);
            return View(existingModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(BankAccount model, CancellationToken cancellationToken)
        {
            var existingModel = await _bankAccountRepo.FindBankAccount(model.BankAccountId, cancellationToken);

            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);

                try
                {
                    existingModel.AccountName = model.AccountName;
                    existingModel.Bank = model.Bank;

                    if (_dbContext.ChangeTracker.HasChanges())
                    {
                        #region --Audit Trail Recording

                        if (model.OriginalBankId == 0)
                        {
                            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                            AuditTrail auditTrailBook = new(createdBy,
                                $"Updated bank {model.AccountName}", "Bank Account", ipAddress!);
                            await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                        }

                        #endregion --Audit Trail Recording

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        TempData["success"] = "Bank edited successfully.";
                        return RedirectToAction(nameof(Index));
                    }

                    throw new InvalidOperationException("No data changes!");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    TempData["error"] = ex.Message;
                    return View(existingModel);
                }
            }
            else
            {
                ModelState.AddModelError("", "The information you submitted is not valid!");
                return View(existingModel);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetBankAccountList(CancellationToken cancellationToken)
        {
            try
            {
                var bankAccounts = await _bankAccountRepo.GetBankAccountAsync(cancellationToken);

                return Json(new
                {
                    data = bankAccounts
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
                var selectedList = await _dbContext.BankAccounts
                    .Where(bank => recordIds.Contains(bank.BankAccountId))
                    .OrderBy(bank => bank.BankAccountId)
                    .ToListAsync(cancellationToken: cancellationToken);

                // Create the Excel package
                using var package = new ExcelPackage();
                // Add a new worksheet to the Excel package
                var worksheet = package.Workbook.Worksheets.Add("BankAccount");

                worksheet.Cells["A1"].Value = "Branch";
                worksheet.Cells["B1"].Value = "CreatedBy";
                worksheet.Cells["C1"].Value = "CreatedDate";
                worksheet.Cells["D1"].Value = "AccountName";
                worksheet.Cells["E1"].Value = "AccountNo";
                worksheet.Cells["F1"].Value = "Bank";
                worksheet.Cells["G1"].Value = "OriginalBankId";

                int row = 2;

                foreach (var item in selectedList)
                {
                    worksheet.Cells[row, 2].Value = item.CreatedBy;
                    worksheet.Cells[row, 3].Value = item.CreatedDate.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 4].Value = item.AccountName;
                    worksheet.Cells[row, 6].Value = item.Bank;
                    worksheet.Cells[row, 7].Value = item.BankAccountId;

                    row++;
                }

                // Convert the Excel package to a byte array
                var excelBytes = await package.GetAsByteArrayAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BankAccountList_IBS-RCD_{DateTime.Now:yyyyddMMHHmmss}.xlsx");
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
            return await WorkbookImport.RunAsync(this, file, "BankAccount", cancellationToken);
        }

        #endregion

        #region -- import xlsx record to AAS --

        [HttpPost]
        public Task<IActionResult> AasImport(IFormFile file, CancellationToken cancellationToken)
            => WorkbookImport.RunAsync(this, file, "BankAccount", cancellationToken, aas: true);

        #endregion
    }
}
