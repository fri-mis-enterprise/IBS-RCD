using Accounting_System.Models;
using Accounting_System.Models.MasterFile;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Controllers
{
    public partial class CustomerController
    {
        private async Task SaveBirDocumentAsync(Customer target, IFormFile? birDocument, CancellationToken token)
        {
            if (birDocument == null || birDocument.Length == 0) return;
            if (birDocument.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("BIR documents must be 10 MB or smaller.");
            var extension = Path.GetExtension(birDocument.FileName).ToLowerInvariant();
            if (extension is not (".pdf" or ".png" or ".jpg" or ".jpeg"))
                throw new InvalidOperationException("Upload a PDF, PNG, or JPEG BIR document.");
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "CustomerDocuments");
            Directory.CreateDirectory(directory);
            var storedName = Guid.NewGuid().ToString("N") + extension;
            await using (var stream = System.IO.File.Create(Path.Combine(directory, storedName)))
                await birDocument.CopyToAsync(stream, token);
            target.BirDocumentFilePath = storedName;
            target.BirDocumentFileName = Path.GetFileName(birDocument.FileName);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadBirDocument(int id, CancellationToken cancellationToken)
        {
            var customer = await _dbContext.Customers.FindAsync([id], cancellationToken);
            if (customer?.BirDocumentFilePath == null) return NotFound();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "CustomerDocuments",
                Path.GetFileName(customer.BirDocumentFilePath));
            if (!System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/octet-stream", customer.BirDocumentFileName);
        }

        [HttpGet]
        public async Task<IActionResult> Branches(int id, CancellationToken cancellationToken)
        {
            var customer = await _dbContext.Customers.Include(c => c.Branches)
                .FirstOrDefaultAsync(c => c.CustomerId == id, cancellationToken);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBranch(CustomerBranch branch, CancellationToken cancellationToken)
        {
            var customer = await _dbContext.Customers.FindAsync([branch.CustomerId], cancellationToken);
            if (customer == null) return NotFound();
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Branch name, address, and TIN are required and must fit their maximum lengths.";
                return RedirectToAction(nameof(Branches), new { id = branch.CustomerId });
            }
            branch.Id = 0;
            _dbContext.CustomerBranches.Add(branch);
            customer.HasBranch = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Branches), new { id = branch.CustomerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBranch(int id, CancellationToken cancellationToken)
        {
            var branch = await _dbContext.CustomerBranches.FindAsync([id], cancellationToken);
            if (branch == null) return NotFound();
            var customer = await _dbContext.Customers.FindAsync([branch.CustomerId], cancellationToken);
            _dbContext.CustomerBranches.Remove(branch);
            customer!.HasBranch = await _dbContext.CustomerBranches.AnyAsync(b => b.CustomerId == branch.CustomerId && b.Id != id, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Branches), new { id = branch.CustomerId });
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerBranches(int id, CancellationToken cancellationToken)
        {
            return Json(await _dbContext.CustomerBranches.Where(b => b.CustomerId == id)
                .Select(b => new { b.Id, b.BranchName, b.BranchAddress, b.BranchTin }).ToListAsync(cancellationToken));
        }
    }
}
