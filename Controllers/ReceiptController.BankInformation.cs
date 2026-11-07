using Accounting_System.Data;
using Accounting_System.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Controllers
{
    public partial class ReceiptController
    {
        [HttpGet]
        public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
            => Json(await _dbContext.BankAccounts.Where(b => b.IsActive)
                .Select(b => new { value = b.BankAccountId, text = b.Bank + " " + b.AccountNo + " " + b.AccountName })
                .ToListAsync(cancellationToken));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Deposit(int id, int bankId, DateOnly depositDate, CancellationToken cancellationToken)
            => UpdateBankInformationAsync(id, "Deposited", bankId, depositDate, cancellationToken);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Redeposit(int id, int bankId, DateOnly redepositDate, CancellationToken cancellationToken)
            => UpdateBankInformationAsync(id, "Redeposited", bankId, redepositDate, cancellationToken);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Return(int id, CancellationToken cancellationToken)
            => UpdateBankInformationAsync(id, "Returned", null, null, cancellationToken);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ApplyClearingDate(int id, DateOnly clearingDate, CancellationToken cancellationToken)
            => UpdateBankInformationAsync(id, "Cleared", null, clearingDate, cancellationToken);

        private async Task<IActionResult> UpdateBankInformationAsync(int id, string operation, int? bankId, DateOnly? date, CancellationToken token)
        {
            var receipt = await _dbContext.CollectionReceipts.FindAsync([id], token);
            if (receipt == null) return NotFound();
            if (!receipt.IsPosted || receipt.IsVoided || receipt.IsCanceled)
                return BadRequest("Only active posted collection receipts can receive bank information.");
            if (operation == "Deposited" && (receipt.DepositedDate != null || receipt.Status == "Returned"))
                return BadRequest("This receipt is already deposited or awaiting redeposit.");
            if (operation == "Redeposited" && receipt.Status != "Returned")
                return BadRequest("Only returned receipts can be redeposited.");
            if (operation is "Returned" or "Cleared" && (receipt.DepositedDate == null || receipt.ClearedDate != null))
                return BadRequest("Only uncleared deposited receipts can be returned or cleared.");
            if (operation != "Returned" && (date == null || date < receipt.TransactionDate))
                return BadRequest("Enter a date on or after the receipt's transaction date.");
            if (operation == "Cleared" && date < receipt.DepositedDate)
                return BadRequest("Clearing cannot precede the deposit date.");
            if (operation is "Deposited" or "Redeposited")
            {
                var bank = await _dbContext.BankAccounts.FirstOrDefaultAsync(b => b.BankAccountId == bankId && b.IsActive, token);
                if (bank == null) return BadRequest("Select an active bank account.");
                receipt.BankId = bank.BankAccountId;
                receipt.BankAccountName = bank.AccountName;
                receipt.BankAccountNumber = bank.AccountNo;
                receipt.DepositedDate = date;
            }
            else if (operation == "Returned") receipt.DepositedDate = null;
            else receipt.ClearedDate = date;
            receipt.Status = operation;
            var user = await _generalRepo.GetUserFullNameAsync(User.Identity!.Name!);
            _dbContext.AuditTrails.Add(new AuditTrail(user, $"{operation} collection receipt #{receipt.CollectionReceiptNo}", "Collection Receipt", HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""));
            // RCD already posts accounting entries in its original Post action. Bank dates add no book entries.
            await _dbContext.SaveChangesAsync(token);
            return Json(new { success = true, status = receipt.Status });
        }
    }
}
