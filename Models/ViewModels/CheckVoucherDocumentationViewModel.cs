using Accounting_System.Models.AccountsPayable;
using System.ComponentModel.DataAnnotations;

namespace Accounting_System.Models.ViewModels
{
    public class CheckVoucherDocumentationViewModel
    {
        [StringLength(100)]
        public string? OldCVNo { get; set; }
        public bool IsDocumentedByOtherCompany { get; set; }
        [StringLength(50)]
        public string? DocumentedByCompanyName { get; set; }
        public DateOnly? DcpDate { get; set; }
        public DateOnly? DcrDate { get; set; }
        public int? VoucherId { get; set; }
        public bool HasSupportingDocument { get; set; }

        public void ApplyTo(CheckVoucherHeader header)
        {
            if (IsDocumentedByOtherCompany && string.IsNullOrWhiteSpace(DocumentedByCompanyName))
                throw new InvalidOperationException("Select the company documenting this voucher.");
            header.OldCvNo = OldCVNo?.Trim();
            header.IsDocumentedByOtherCompany = IsDocumentedByOtherCompany;
            header.DocumentedByCompanyName = IsDocumentedByOtherCompany ? DocumentedByCompanyName?.Trim() : null;
            header.DcpDate = DcpDate;
            header.DcrDate = DcrDate;
        }

        public static CheckVoucherDocumentationViewModel From(CheckVoucherHeader header) => new()
        {
            VoucherId = header.CheckVoucherHeaderId,
            HasSupportingDocument = !string.IsNullOrWhiteSpace(header.SupportingFileSavedUrl),
            OldCVNo = header.OldCvNo,
            IsDocumentedByOtherCompany = header.IsDocumentedByOtherCompany == true,
            DocumentedByCompanyName = header.DocumentedByCompanyName,
            DcpDate = header.DcpDate,
            DcrDate = header.DcrDate
        };
    }
}
