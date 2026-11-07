using System.Globalization;
using Accounting_System.Data;
using Accounting_System.Models.MasterFile;
using Accounting_System.Models.Reports;
using Accounting_System.Repository;
using Accounting_System.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly AasDbContext _aasDbContext;

        private readonly ProductRepository _productRepository;

        private readonly GeneralRepo _generalRepo;

        public ProductController(ApplicationDbContext dbContext, ProductRepository productRepository, AasDbContext aasDbContext,GeneralRepo generalRepo)
        {
            _dbContext = dbContext;
            _productRepository = productRepository;
            _aasDbContext = aasDbContext;
            _generalRepo = generalRepo;
        }

        public async Task<IActionResult> Index(string? view, CancellationToken cancellationToken)
        {
            var data = await _dbContext.Products.ToListAsync(cancellationToken);

            if (view == nameof(DynamicView.Product))
            {
                return View("ImportExportIndex");
            }

            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProductIds(CancellationToken cancellationToken)
        {
            var productIds = await _dbContext.Products
                                     .Select(p => p.ProductId) // Assuming Id is the primary key
                                     .ToListAsync(cancellationToken);
            return Json(productIds);
        }

        public IActionResult Create()
        {
            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                try
                {
                    if (await _productRepository.IsProductCodeExist(product.ProductCode!, cancellationToken))
                    {
                        ModelState.AddModelError("Code", "Product code already exist!");
                        return View(product);
                    }

                    if (await _productRepository.IsProductNameExist(product.ProductName, cancellationToken))
                    {
                        ModelState.AddModelError("Name", "Product name already exist!");
                        return View(product);
                    }

                    product.CreatedBy = createdBy;

                    #region --Audit Trail Recording

                    if (product.OriginalProductId == 0)
                    {
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        AuditTrail auditTrailBook = new(createdBy, $"Created new product {product.ProductName}", "Product", ipAddress!);
                        await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                    }

                    #endregion --Audit Trail Recording

                    await _dbContext.AddAsync(product, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    TempData["success"] = "Product created successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    TempData["error"] = ex.Message;
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(product);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
        {
            if (id == null || !_dbContext.Products.Any())
            {
                return NotFound();
            }

            var product = await _dbContext.Products.FirstOrDefaultAsync(x => x.ProductId == id, cancellationToken);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, CancellationToken cancellationToken)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createdBy = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
                try
                {
                    var existingProduct = await _dbContext.Products.FirstOrDefaultAsync(x => x.ProductId == product.ProductId, cancellationToken);
                    existingProduct!.ProductCode = product.ProductCode;
                    existingProduct.ProductName = product.ProductName;
                    existingProduct.ProductUnit = product.ProductUnit;
                    existingProduct.IsActive = product.IsActive;
                    existingProduct.EditedBy = createdBy;
                    existingProduct.EditedDate = DateTime.Now;
                    existingProduct.ProductId = product.ProductId;


                    if (_dbContext.ChangeTracker.HasChanges())
                    {
                        #region --Audit Trail Recording

                        if (product.OriginalProductId == 0)
                        {
                            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                            AuditTrail auditTrailBook = new(createdBy, $"Updated product {product.ProductName}", "Product", ipAddress!);
                            await _dbContext.AddAsync(auditTrailBook, cancellationToken);
                        }

                        #endregion --Audit Trail Recording

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        TempData["success"] = "Product updated successfully";
                    }
                    else
                    {
                        throw new InvalidOperationException("No data changes!");
                    }

                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (!ProductExists(product.ProductId))
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
                    return View(product);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        private bool ProductExists(int id)
        {
            return _dbContext.Products != null! && _dbContext.Products.Any(e => e.ProductId == id);
        }

        [HttpPost]
        public async Task<IActionResult> GetProductList(CancellationToken cancellationToken)
        {
            try
            {
                var products = await _dbContext.Products.ToListAsync(cancellationToken);

                return Json(new
                {
                    data = products
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
                var selectedList = await _dbContext.Products
                    .Where(p => recordIds.Contains(p.ProductId))
                    .OrderBy(p => p.ProductId)
                    .ToListAsync(cancellationToken: cancellationToken);

                // Create the Excel package
                using var package = new ExcelPackage();
                // Add a new worksheet to the Excel package
                var worksheet = package.Workbook.Worksheets.Add("Product");

                worksheet.Cells["A1"].Value = "ProductCode";
                worksheet.Cells["B1"].Value = "ProductName";
                worksheet.Cells["C1"].Value = "ProductUnit";
                worksheet.Cells["D1"].Value = "CreatedBy";
                worksheet.Cells["E1"].Value = "CreatedDate";
                worksheet.Cells["F1"].Value = "OriginalProductId";

                var addedHeaders = new[] { "IsActive", "EditedBy", "EditedDate" };
                var addedStartColumn = worksheet.Dimension.Columns + 1;
                for (var c = 0; c < addedHeaders.Length; c++) worksheet.Cells[1, addedStartColumn + c].Value = addedHeaders[c];

                int row = 2;

                foreach (var item in selectedList)
                {
                    worksheet.Cells[row, 1].Value = item.ProductCode;
                    worksheet.Cells[row, 2].Value = item.ProductName;
                    worksheet.Cells[row, 3].Value = item.ProductUnit;
                    worksheet.Cells[row, 4].Value = item.CreatedBy;
                    worksheet.Cells[row, 5].Value = item.CreatedDate.ToString(CS.ImportDateTimeFormat);
                    worksheet.Cells[row, 6].Value = item.ProductId;

                    object?[] addedValues = [item.IsActive, item.EditedBy, item.EditedDate];
                    for (var c = 0; c < addedValues.Length; c++) worksheet.Cells[row, addedStartColumn + c].Value = addedValues[c];
                    row++;
                }

                // Convert the Excel package to a byte array
                var excelBytes = await package.GetAsByteArrayAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ProductList_IBS-RCD_{DateTime.Now:yyyyddMMHHmmss}.xlsx");
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
            return await WorkbookImport.RunAsync(this, file, "Product", cancellationToken);
        }

        #endregion

        //Upload as .xlsx file.(Import)
        #region -- import xlsx record to AAS --

        [HttpPost]
        public Task<IActionResult> AasImport(IFormFile file, CancellationToken cancellationToken)
            => WorkbookImport.RunAsync(this, file, "Product", cancellationToken, aas: true);

        #endregion
    }
}
