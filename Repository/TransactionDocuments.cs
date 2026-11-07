using Accounting_System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Repository
{
    public static class TransactionDocuments
    {
        public static async Task<IActionResult> DownloadVoucherAsync(ApplicationDbContext db, string webRoot, int id, CancellationToken token)
        {
            var voucher = await db.CheckVoucherHeaders.AsNoTracking().FirstOrDefaultAsync(v => v.CheckVoucherHeaderId == id, token);
            if (string.IsNullOrWhiteSpace(voucher?.SupportingFileSavedUrl)) return new NotFoundResult();
            if (!Path.IsPathFullyQualified(voucher.SupportingFileSavedUrl)) return new NotFoundResult();
            var path = Path.GetFullPath(voucher.SupportingFileSavedUrl);
            var root = Path.GetFullPath(webRoot) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return new NotFoundResult();
            return new PhysicalFileResult(path, "application/octet-stream")
            {
                FileDownloadName = voucher.SupportingFileSavedFileName ?? Path.GetFileName(path)
            };
        }
    }
}
