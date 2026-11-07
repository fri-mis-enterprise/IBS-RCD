using Accounting_System.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Accounting_System.Controllers
{
    public partial class CheckVoucherTradeController
    {
        [HttpGet]
        public Task<IActionResult> SupportingDocument(int id, CancellationToken cancellationToken)
            => TransactionDocuments.DownloadVoucherAsync(_dbContext, _webHostEnvironment.WebRootPath, id, cancellationToken);
    }
}
