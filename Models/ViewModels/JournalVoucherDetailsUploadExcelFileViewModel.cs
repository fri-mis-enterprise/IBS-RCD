using Accounting_System.Models.MasterFile;
namespace Accounting_System.Models.ViewModels
{
    public class JournalVoucherDetailsUploadExcelFileViewModel
    {
        public string AccountNo { get; set; } = " ";

        public string AccountName { get; set; } = " ";

        public string TransactionNo { get; set; } = " ";

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }

        public int JournalVoucherHeaderId { get; set; }

        public int OriginalDocumentId { get; set; }

        public SubAccountType? SubAccountType { get; set; }

        public int? SubAccountId { get; set; }

        public string? SubAccountName { get; set; }
    }
}
