using System.Globalization;
using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsReceivable;
using Accounting_System.Models.Reports;
using Accounting_System.Repository;
using Accounting_System.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Linq.Dynamic.Core;
using Microsoft.IdentityModel.Tokens;

namespace Accounting_System.Controllers
{
    [Authorize]
    public class ServiceInvoiceController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly AasDbContext _aasDbContext;

        private readonly ServiceInvoiceRepo _serviceInvoiceRepo;

        private readonly UserManager<IdentityUser> _userManager;

        private readonly GeneralRepo _generalRepo;

        public ServiceInvoiceController(ApplicationDbContext dbContext, ServiceInvoiceRepo statementOfAccountRepo, UserManager<IdentityUser> userManager, GeneralRepo generalRepo, AasDbContext aasDbContext)
        {
            _dbContext = dbContext;
            _serviceInvoiceRepo = statementOfAccountRepo;
            _userManager = userManager;
            _generalRepo = generalRepo;
            _aasDbContext = aasDbContext;
        }

        public IActionResult Index(string? view)
        {
            if (view == nameof(DynamicView.ServiceInvoice))
            {
                return View("ImportExportIndex");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetServiceInvoices([FromForm] DataTablesParameters parameters, CancellationToken cancellationToken)
        {
            try
            {
                var serviceInvoices = await _serviceInvoiceRepo.GetServiceInvoicesAsync(cancellationToken);
                // Search filter
                if (!string.IsNullOrEmpty(parameters.Search.Value))
                {
                    var searchValue = parameters.Search.Value.ToLower();
                    serviceInvoices = serviceInvoices
                        .Where(sv =>
                            sv.ServiceInvoiceNo!.ToLower().Contains(searchValue) ||
                            sv.Customer!.CustomerName.ToLower().Contains(searchValue) ||
                            sv.Customer.CustomerTerms.ToLower().Contains(searchValue) ||
                            sv.Service!.Name.ToLower().Contains(searchValue) ||
                            sv.Service.ServiceNo.ToString().Contains(searchValue) ||
                            sv.Period.ToString("MMM yyyy").ToLower().Contains(searchValue) ||
                            sv.Amount.ToString(CultureInfo.InvariantCulture).Contains(searchValue) ||
                            sv.Instructions?.ToLower().Contains(searchValue) == true ||
                            sv.CreatedBy!.ToLower().Contains(searchValue)
                            )
                        .ToList();
                }
                // Sorting
                if (parameters.Order != null && parameters.Order.Count > 0)
                {
                    var orderColumn = parameters.Order[0];
                    var columnName = parameters.Columns[orderColumn.Column].Data;
                    var sortDirection = orderColumn.Dir.ToLower() == "asc" ? "ascending" : "descending";
                    serviceInvoices = serviceInvoices
                        .AsQueryable()
                        .OrderBy($"{columnName} {sortDirection}")
                        .ToList();
                }
                var totalRecords = serviceInvoices.Count();
                var pagedData = serviceInvoices
                    .Skip(parameters.Start)
                    .Take(parameters.Length)
                    .ToList();
                return Json(new
                {
                    draw = parameters.Draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = pagedData
                });
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllServiceInvoiceIds(CancellationToken  cancellationToken)
        {
            var invoiceIds = await _dbContext.ServiceInvoices
                                     .Select(sv => sv.ServiceInvoiceId) // Assuming Id is the primary key
                                     .ToListAsync(cancellationToken);
            return Json(invoiceIds);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var viewModel = new ServiceInvoice
            {
                Customers = await _dbContext.Customers.Where(x => x.IsActive)
                    .OrderBy(c => c.CustomerId)
                    .Select(c => new SelectListItem
                    {
                        Value = c.CustomerId.ToString(),
                        Text = c.CustomerName
                    })
                    .ToListAsync(cancellationToken),
                Services = await _dbContext.Services
                    .OrderBy(s => s.ServiceId)
                    .Select(s => new SelectListItem
                    {
                        Value = s.ServiceId.ToString(),
                        Text = s.Name
                    })
                    .ToListAsync(cancellationToken)
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ServiceInvoice model, CancellationToken cancellationToken)
        {
            model.Customers = await _dbContext.Customers.Where(x => x.IsActive)
                .OrderBy(c => c.CustomerId)
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = c.CustomerName
                })
                .ToListAsync(cancellationToken);
            model.Services = await _dbContext.Services
                .OrderBy(s => s.ServiceId)
                .Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = s.Name
                })
                .ToListAsync(cancellationToken);
            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                try
                {
                    #region --Validating the series

                    var generateSvNo = await _serviceInvoiceRepo.GenerateSvNo(cancellationToken);
                    var getLastNumber = long.Parse(generateSvNo.Substring(2));

                    if (getLastNumber > 9999999999)
                    {
                        TempData["error"] = "You reach the maximum Series Number";
                        return View(model);
                    }
                    var totalRemainingSeries = 9999999999 - getLastNumber;
                    if (getLastNumber >= 9999999899)
                    {
                        TempData["warning"] = $"Service invoice created successfully, Warning {totalRemainingSeries} series number remaining";
                    }
                    else
                    {
                        TempData["success"] = "Service invoice created successfully";
                    }

                    #endregion --Validating the series

                    #region --Retrieval of Services

                    var services = await _serviceInvoiceRepo.GetServicesAsync(model.ServicesId, cancellationToken);

                    #endregion --Retrieval of Services

                    #region --Retrieval of Customer

                    var customer = await _serviceInvoiceRepo.FindCustomerAsync(model.CustomerId, cancellationToken);
                    model.CwtPercent = customer.CwtPercent;
                    model.CwVatPercent = customer.CwVatPercent;

                    #endregion --Retrieval of Customer

                    #region --Saving the default properties

                    model.ServiceInvoiceNo = generateSvNo;

                    model.CreatedBy = createdBy;

                    model.ServiceNo = services.ServiceNo;

                    model.Total = model.Amount;

                    if (DateOnly.FromDateTime(model.CreatedDate) < model.Period)
                    {
                        model.UnearnedAmount += model.Amount;
                    }
                    else
                    {
                        model.CurrentAndPreviousAmount += model.Amount;
                    }

                    if (customer.VatType == "Vatable")
                    {
                        model.CurrentAndPreviousAmount = Math.Round(model.CurrentAndPreviousAmount / 1.12m, 2);
                        model.UnearnedAmount = Math.Round(model.UnearnedAmount / 1.12m, 2);

                        var total = model.CurrentAndPreviousAmount + model.UnearnedAmount;

                        var netOfVatAmount = _generalRepo.ComputeNetOfVat(model.Amount);
                        var roundedNetAmount = Math.Round(netOfVatAmount, 2);

                        if (roundedNetAmount > total)
                        {
                            var shortAmount = netOfVatAmount - total;

                            model.CurrentAndPreviousAmount += shortAmount;
                        }
                    }

                    await _dbContext.AddAsync(model, cancellationToken);

                    #endregion --Saving the default properties

                    #region --Audit Trail Recording

                    if (model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId == 0)
                    {
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        AuditTrail auditTrailBook = new(createdBy, $"Create new service invoice# {model.ServiceInvoiceNo}", "Service Invoice", ipAddress!);
                        await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                    }

                    #endregion --Audit Trail Recording

                    await TransactionDraftFields.PrepareAsync(_dbContext, cancellationToken);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                 await transaction.RollbackAsync(cancellationToken);
                 TempData["error"] = ex.Message;
                 return RedirectToAction(nameof(Index));
                }
            }

            return View(model);
        }

        public async Task<IActionResult> PrintInvoice(int id, CancellationToken cancellationToken)
        {
            var soa = await _serviceInvoiceRepo
                .FindSv(id, cancellationToken);

            return View(soa);
        }

        public async Task<IActionResult> PrintedInvoice(int id, CancellationToken cancellationToken)
        {
            var findIdOfSoa = await _serviceInvoiceRepo.FindSv(id, cancellationToken);
            var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
            if (!findIdOfSoa.IsPrinted)
            {

                #region --Audit Trail Recording

                if (findIdOfSoa.OriginalSeriesNumber.IsNullOrEmpty() && findIdOfSoa.OriginalDocumentId == 0)
                {
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                    AuditTrail auditTrailBook = new(createdBy, $"Printed original copy of sv# {findIdOfSoa.ServiceInvoiceNo}", "Service Invoice", ipAddress!);
                    await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                }

                #endregion --Audit Trail Recording

                findIdOfSoa.IsPrinted = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return RedirectToAction(nameof(PrintInvoice), new { id });
        }

        public async Task<IActionResult> Post(int id, CancellationToken cancellationToken)
        {
            var model = await _serviceInvoiceRepo.FindSv(id, cancellationToken);
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var userName = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
            var postedBy = !model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId != 0 ? model.PostedBy : userName;
            var date = !model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId != 0 ? model.PostedDate : DateTime.Now;
            try
            {
                if (!model.IsPosted)
                {
                    model.IsPosted = true;
                    model.PostedBy = postedBy;
                    model.PostedDate = date;

                    #region --SV Date Computation--

                    var postedDate = DateOnly.FromDateTime(model.CreatedDate) >= model.Period ? DateOnly.FromDateTime(model.CreatedDate) : model.Period.AddMonths(1).AddDays(-1);

                    #endregion --SV Date Computation--

                    #region --Sales Book Recording

                    decimal withHoldingTaxAmount = 0;
                    decimal withHoldingVatAmount = 0;
                    decimal netOfVatAmount;
                    decimal vatAmount = 0;

                    if (model.Customer!.VatType == CS.VatType_Vatable)
                    {
                        netOfVatAmount = _generalRepo.ComputeNetOfVat(model.Total);
                        vatAmount = _generalRepo.ComputeVatAmount(netOfVatAmount);
                    }
                    else
                    {
                        netOfVatAmount = model.Total;
                    }

                    if (model.Customer.WithHoldingTax)
                    {
                        withHoldingTaxAmount = _generalRepo.ComputeEwtAmount(netOfVatAmount, model.CwtPercent);
                    }

                    if (model.Customer.WithHoldingVat)
                    {
                        withHoldingVatAmount = _generalRepo.ComputeEwtAmount(netOfVatAmount, model.CwVatPercent);
                    }

                    var sales = new SalesBook();

                    if (model.Customer.VatType == "Vatable")
                    {
                        sales.TransactionDate = postedDate;
                        sales.SerialNo = model.ServiceInvoiceNo!;
                        sales.SoldTo = model.Customer.CustomerName;
                        sales.TinNo = model.Customer.CustomerTin;
                        sales.Address = model.Customer.CustomerAddress;
                        sales.Description = model.Service!.Name;
                        sales.Amount = model.Total;
                        sales.VatAmount = vatAmount;
                        sales.VatableSales = netOfVatAmount;
                        sales.Discount = model.Discount;
                        sales.NetSales = netOfVatAmount;
                        sales.CreatedBy = model.CreatedBy;
                        sales.CreatedDate = model.CreatedDate;
                        sales.DueDate = model.DueDate;
                        sales.DocumentId = model.ServiceInvoiceId;
                    }
                    else if (model.Customer.VatType == "Exempt")
                    {
                        sales.TransactionDate = postedDate;
                        sales.SerialNo = model.ServiceInvoiceNo!;
                        sales.SoldTo = model.Customer.CustomerName;
                        sales.TinNo = model.Customer.CustomerTin;
                        sales.Address = model.Customer.CustomerAddress;
                        sales.Description = model.Service!.Name;
                        sales.Amount = model.Total;
                        sales.VatExemptSales = model.Total;
                        sales.Discount = model.Discount;
                        sales.NetSales = netOfVatAmount;
                        sales.CreatedBy = model.CreatedBy;
                        sales.CreatedDate = model.CreatedDate;
                        sales.DueDate = model.DueDate;
                        sales.DocumentId = model.ServiceInvoiceId;
                    }
                    else
                    {
                        sales.TransactionDate = postedDate;
                        sales.SerialNo = model.ServiceInvoiceNo!;
                        sales.SoldTo = model.Customer.CustomerName;
                        sales.TinNo = model.Customer.CustomerTin;
                        sales.Address = model.Customer.CustomerAddress;
                        sales.Description = model.Service!.Name;
                        sales.Amount = model.Total;
                        sales.ZeroRated = model.Total;
                        sales.Discount = model.Discount;
                        sales.NetSales = netOfVatAmount;
                        sales.CreatedBy = model.CreatedBy;
                        sales.CreatedDate = model.CreatedDate;
                        sales.DueDate = model.DueDate;
                        sales.DocumentId = model.ServiceInvoiceId;
                    }

                    await _dbContext.AddAsync(sales, cancellationToken);

                    #endregion --Sales Book Recording

                    #region --General Ledger Book Recording

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
                                Reference = model.ServiceInvoiceNo!,
                                Description = model.Service.Name,
                                AccountNo = arNonTradeTitle.AccountNumber,
                                AccountTitle = arNonTradeTitle.AccountName,
                                Debit = Math.Round(model.Total - (withHoldingTaxAmount + withHoldingVatAmount), 2),
                                Credit = 0,
                                CreatedBy = model.CreatedBy,
                                CreatedDate = model.CreatedDate
                            }
                        );
                    if (withHoldingTaxAmount > 0)
                    {
                        ledgers.Add(
                            new GeneralLedgerBook
                            {
                                Date = postedDate,
                                Reference = model.ServiceInvoiceNo!,
                                Description = model.Service.Name,
                                AccountNo = arTradeCwt.AccountNumber,
                                AccountTitle = arTradeCwt.AccountName,
                                Debit = withHoldingTaxAmount,
                                Credit = 0,
                                CreatedBy = model.CreatedBy,
                                CreatedDate = model.CreatedDate
                            }
                        );
                    }
                    if (withHoldingVatAmount > 0)
                    {
                        ledgers.Add(
                            new GeneralLedgerBook
                            {
                                Date = postedDate,
                                Reference = model.ServiceInvoiceNo!,
                                Description = model.Service.Name,
                                AccountNo = arTradeCwv.AccountNumber,
                                AccountTitle = arTradeCwv.AccountName,
                                Debit = withHoldingVatAmount,
                                Credit = 0,
                                CreatedBy = model.CreatedBy,
                                CreatedDate = model.CreatedDate
                            }
                        );
                    }

                    ledgers.Add(
                           new GeneralLedgerBook
                           {
                               Date = postedDate,
                               Reference = model.ServiceInvoiceNo!,
                               Description = model.Service.Name,
                               AccountNo = model.Service.CurrentAndPreviousNo!,
                               AccountTitle = model.Service.CurrentAndPreviousTitle!,
                               Debit = 0,
                               Credit = Math.Round((netOfVatAmount), 2),
                               CreatedBy = model.CreatedBy,
                               CreatedDate = model.CreatedDate
                           }
                       );

                    if (vatAmount > 0)
                    {
                        ledgers.Add(
                            new GeneralLedgerBook
                            {
                                Date = postedDate,
                                Reference = model.ServiceInvoiceNo!,
                                Description = model.Service.Name,
                                AccountNo = vatOutputTitle.AccountNumber,
                                AccountTitle = vatOutputTitle.AccountName,
                                Debit = 0,
                                Credit = Math.Round((vatAmount), 2),
                                CreatedBy = model.CreatedBy,
                                CreatedDate = model.CreatedDate
                            }
                        );
                    }

                    if (!_generalRepo.IsJournalEntriesBalanced(ledgers))
                    {
                        throw new ArgumentException("Debit and Credit is not equal, check your entries.");
                    }

                    await _dbContext.GeneralLedgerBooks.AddRangeAsync(ledgers, cancellationToken);

                    #endregion --General Ledger Book Recording

                    #region --Audit Trail Recording

                    if (model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId == 0)
                    {
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        AuditTrail auditTrailBook = new(postedBy, $"Posted service invoice# {model.ServiceInvoiceNo}", "Service Invoice", ipAddress!);
                        await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                    }

                    #endregion --Audit Trail Recording

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    TempData["success"] = "Service invoice has been posted.";
                    return RedirectToAction(nameof(PrintInvoice), new { id });
                }

                return RedirectToAction(nameof(PrintInvoice), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(PrintInvoice), new { id });
            }
        }

        public async Task<IActionResult> Cancel(int id, string cancellationRemarks, CancellationToken cancellationToken)
        {
            var model = await _dbContext.ServiceInvoices.FirstOrDefaultAsync(x => x.ServiceInvoiceId == id, cancellationToken);
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var canceledBy = !model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId != 0 ? model.CanceledBy : await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
            var date = !model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId != 0 ? model.CanceledDate : DateTime.Now;
            try
            {
                if (model != null)
                {
                    if (!model.IsCanceled)
                    {
                        model.IsCanceled = true;
                        model.CanceledBy = canceledBy;
                        model.CanceledDate = date;
                        model.CancellationRemarks = cancellationRemarks;

                        #region --Audit Trail Recording

                        if (model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId == 0)
                        {
                            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                            AuditTrail auditTrailBook = new(canceledBy, $"Cancelled service invoice# {model.ServiceInvoiceNo}", "Service Invoice", ipAddress!);
                            await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                        }

                        #endregion --Audit Trail Recording

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        TempData["success"] = "Service invoice has been Cancelled.";
                    }
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            return NotFound();
        }

        public async Task<IActionResult> Void(int id, CancellationToken cancellationToken)
        {
            var model = await _dbContext.ServiceInvoices.FirstOrDefaultAsync(x => x.ServiceInvoiceId == id, cancellationToken);
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var voidedBy = !model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId != 0 ? model.VoidedBy : await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
            var date = !model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId != 0 ? model.VoidedDate : DateTime.Now;
            try
            {
                if (model != null)
                {
                    if (!model.IsVoided)
                    {
                        if (model.IsPosted)
                        {
                            model.IsPosted = false;
                        }

                        model.IsVoided = true;
                        model.VoidedBy = voidedBy;
                        model.VoidedDate = date;

                        #region --Audit Trail Recording

                        if (model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId == 0)
                        {
                            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                            AuditTrail auditTrailBook = new(voidedBy, $"Voided service invoice# {model.ServiceInvoiceNo}", "Service Invoice", ipAddress!);
                            await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                        }

                        #endregion --Audit Trail Recording

                        await _generalRepo.RemoveRecords<SalesBook>(gl => gl.SerialNo == model.ServiceInvoiceNo, cancellationToken);
                        await _generalRepo.RemoveRecords<GeneralLedgerBook>(gl => gl.Reference == model.ServiceInvoiceNo, cancellationToken);

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        TempData["success"] = "Service invoice has been voided.";
                    }
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            if (id == 0)
            {
                return NotFound();
            }
            var existingModel = await _serviceInvoiceRepo.FindSv(id, cancellationToken);

            existingModel.Customers = await _dbContext.Customers.Where(x => x.IsActive)
                .OrderBy(c => c.CustomerId)
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = c.CustomerName
                })
                .ToListAsync(cancellationToken);
            existingModel.Services = await _dbContext.Services
                .OrderBy(s => s.ServiceId)
                .Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = s.Name
                })
                .ToListAsync(cancellationToken);

            return View(existingModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ServiceInvoice model, CancellationToken cancellationToken)
        {
            var existingModel = await _serviceInvoiceRepo.FindSv(model.ServiceInvoiceId, cancellationToken);

            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                try
                {
                    #region --Saving the default properties

                    existingModel.Discount = model.Discount;
                    existingModel.Amount = model.Amount;
                    existingModel.Period = model.Period;
                    existingModel.DueDate = model.DueDate;
                    existingModel.Instructions = model.Instructions;

                    decimal total = 0;
                    total += model.Amount;
                    existingModel.Total = total;

                    #endregion --Saving the default properties

                    if (_dbContext.ChangeTracker.HasChanges())
                    {
                        #region --Audit Trail Recording

                        if (model.OriginalSeriesNumber.IsNullOrEmpty() && model.OriginalDocumentId == 0)
                        {
                            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                            AuditTrail auditTrailBook = new(createdBy, $"Edited service invoice# {existingModel.ServiceInvoiceNo}", "Service Invoice", ipAddress!);
                            await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                        }

                        #endregion --Audit Trail Recording

                        await TransactionDraftFields.PrepareAsync(_dbContext, cancellationToken);

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        TempData["success"] = "Service Invoice updated successfully";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        throw new InvalidOperationException("No data changes!");
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    existingModel.Customers = await _dbContext.Customers.Where(x => x.IsActive)
                        .OrderBy(c => c.CustomerId)
                        .Select(c => new SelectListItem
                        {
                            Value = c.CustomerId.ToString(),
                            Text = c.CustomerName
                        })
                        .ToListAsync(cancellationToken);
                    existingModel.Services = await _dbContext.Services
                        .OrderBy(s => s.ServiceId)
                        .Select(s => new SelectListItem
                        {
                            Value = s.ServiceId.ToString(),
                            Text = s.Name
                        })
                        .ToListAsync(cancellationToken);
                    TempData["error"] = ex.Message;
                    return View(existingModel);
                }
            }

            existingModel.Customers = await _dbContext.Customers.Where(x => x.IsActive)
                .OrderBy(c => c.CustomerId)
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = c.CustomerName
                })
                .ToListAsync(cancellationToken);
            existingModel.Services = await _dbContext.Services
                .OrderBy(s => s.ServiceId)
                .Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = s.Name
                })
                .ToListAsync(cancellationToken);
            return View(existingModel);
        }

        [HttpPost]
        public async Task<IActionResult> GetServiceInvoiceList(CancellationToken cancellationToken)
        {
            try
            {
                var serviceInvoices = await _serviceInvoiceRepo.GetServiceInvoicesAsync(cancellationToken);

                return Json(new
                {
                    data = serviceInvoices
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
                var selectedList = await _dbContext.ServiceInvoices
                    .Where(sv => recordIds.Contains(sv.ServiceInvoiceId))
                    .OrderBy(sv => sv.ServiceInvoiceNo)
                    .ToListAsync(cancellationToken: cancellationToken);

                // Create the Excel package
                using var package = new ExcelPackage();
                // Add a new worksheet to the Excel package
                var worksheet = package.Workbook.Worksheets.Add("ServiceInvoice");

                worksheet.Cells["A1"].Value = "DueDate";
                worksheet.Cells["B1"].Value = "Period";
                worksheet.Cells["C1"].Value = "Amount";
                worksheet.Cells["D1"].Value = "Total";
                worksheet.Cells["E1"].Value = "Discount";
                worksheet.Cells["F1"].Value = "CurrentAndPreviousMonth";
                worksheet.Cells["G1"].Value = "UnearnedAmount";
                worksheet.Cells["H1"].Value = "Status";
                worksheet.Cells["I1"].Value = "AmountPaid";
                worksheet.Cells["J1"].Value = "Balance";
                worksheet.Cells["K1"].Value = "Instructions";
                worksheet.Cells["L1"].Value = "IsPaid";
                worksheet.Cells["M1"].Value = "CreatedBy";
                worksheet.Cells["N1"].Value = "CreatedDate";
                worksheet.Cells["O1"].Value = "CancellationRemarks";
                worksheet.Cells["P1"].Value = "OriginalCustomerId";
                worksheet.Cells["Q1"].Value = "OriginalSVNo";
                worksheet.Cells["R1"].Value = "OriginalServicesId";
                worksheet.Cells["S1"].Value = "OriginalDocumentId";
                worksheet.Cells["T1"].Value = "PostedBy";
                worksheet.Cells["U1"].Value = "PostedDate";
                worksheet.Cells["V1"].Value = "EditedBy";
                worksheet.Cells["W1"].Value = "EditedDate";
                worksheet.Cells["X1"].Value = "CanceledBy";
                worksheet.Cells["Y1"].Value = "CanceledDate";
                worksheet.Cells["Z1"].Value = "VoidedBy";
                worksheet.Cells["AA1"].Value = "VoidedDate";

                var rateColumn = worksheet.Dimension.Columns + 1;
                worksheet.Cells[1, rateColumn].Value = "CwtPercent";
                worksheet.Cells[1, rateColumn + 1].Value = "CwVatPercent";
                int row = 2;

                foreach (var item in selectedList)
                {
                    worksheet.Cells[row, 1].Value = item.DueDate.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 2].Value = item.Period.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 3].Value = item.Amount;
                    worksheet.Cells[row, 4].Value = item.Total;
                    worksheet.Cells[row, 5].Value = item.Discount;
                    worksheet.Cells[row, 6].Value = item.CurrentAndPreviousAmount;
                    worksheet.Cells[row, 7].Value = item.UnearnedAmount;
                    worksheet.Cells[row, 8].Value = item.Status;
                    worksheet.Cells[row, 9].Value = item.AmountPaid;
                    worksheet.Cells[row, 10].Value = item.Balance;
                    worksheet.Cells[row, 11].Value = item.Instructions;
                    worksheet.Cells[row, 12].Value = item.IsPaid;
                    worksheet.Cells[row, 13].Value = item.CreatedBy;
                    worksheet.Cells[row, 14].Value = item.CreatedDate.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 15].Value = item.CancellationRemarks;
                    worksheet.Cells[row, 16].Value = item.CustomerId;
                    worksheet.Cells[row, 17].Value = item.ServiceInvoiceNo;
                    worksheet.Cells[row, 18].Value = item.ServicesId;
                    worksheet.Cells[row, 19].Value = item.ServiceInvoiceId;
                    worksheet.Cells[row, 20].Value = item.PostedBy;
                    worksheet.Cells[row, 21].Value = item.PostedDate?.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 22].Value = item.EditedBy;
                    worksheet.Cells[row, 23].Value = item.EditedDate?.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 24].Value = item.CanceledBy;
                    worksheet.Cells[row, 25].Value = item.CanceledDate?.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 26].Value = item.VoidedBy;
                    worksheet.Cells[row, 27].Value = item.VoidedDate?.ToString(CS.ImportDateTimeFormat);

                    worksheet.Cells[row, rateColumn].Value = item.CwtPercent;
                    worksheet.Cells[row, rateColumn + 1].Value = item.CwVatPercent;
                    row++;
                }

                // Convert the Excel package to a byte array
                TransactionColumns.Export(worksheet, selectedList);
                var excelBytes = await package.GetAsByteArrayAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ServiceInvoiceList_IBS-RCD_{DateTime.Now:yyyyddMMHHmmss}.xlsx");
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
            return await WorkbookImport.RunAsync(this, file, "ServiceInvoice", cancellationToken);
        }

        #endregion

        //Upload as .xlsx file.(Import)
        #region -- import xlsx record to AAS --

        [HttpPost]
        public Task<IActionResult> AasImport(IFormFile file, CancellationToken cancellationToken)
            => WorkbookImport.RunAsync(this, file, "ServiceInvoice", cancellationToken, aas: true);
        #endregion
    }
}
