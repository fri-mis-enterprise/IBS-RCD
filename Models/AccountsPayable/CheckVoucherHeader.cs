using Accounting_System.Models.MasterFile;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accounting_System.Models.AccountsPayable
{
    public class CheckVoucherHeader : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CheckVoucherHeaderId { get; set; }

        [Display(Name = "CV No")]
        [StringLength(13)]
        public string? CheckVoucherHeaderNo { get; set; }

        [Display(Name = "Transaction Date")]
        [Column(TypeName = "date")]
        public DateOnly Date { get; set; }

        [Display(Name = "RR No")]
        [Column(TypeName = "varchar[]")]
        public string[]? RRNo { get; set; }

        [Display(Name = "SI No")]
        [Column(TypeName = "varchar[]")]
        public string[]? SINo { get; set; }

        [NotMapped]
        public List<SelectListItem>? RR { get; set; }

        [Display(Name = "PO No")]
        [Column(TypeName = "varchar[]")]
        public string[]? PONo { get; set; }

        [NotMapped]
        public List<SelectListItem>? PO { get; set; }

        [Display(Name = "Supplier Id")]
        public int? SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [NotMapped]
        public List<SelectListItem>? Suppliers { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal Total { get; set; }

        [Column(TypeName = "numeric(18,4)[]")]
        public decimal[]? Amount { get; set; }

        [StringLength(1000)]
        public string? Particulars { get; set; }

        [Display(Name = "Bank Account Name")]
        public int? BankId { get; set; }

        [ForeignKey(nameof(BankId))]
        public BankAccount? BankAccount { get; set; }

        [Display(Name = "Check #")]
        [StringLength(50)]
        public string? CheckNo { get; set; }

        [StringLength(20)]
        public string Category { get; set; }

        [Display(Name = "Payee")]
        [StringLength(150)]
        public string? Payee { get; set; }

        [NotMapped]
        public List<SelectListItem>? BankAccounts { get; set; }

        [NotMapped]
        public List<SelectListItem>? COA { get; set; }

        [Display(Name = "Check Date")]
        [Column(TypeName = "date")]
        public DateOnly? CheckDate { get; set; }

        [Display(Name = "Start Date:")]
        [Column(TypeName = "date")]
        public DateOnly? StartDate { get; set; }

        [Display(Name = "End Date:")]
        [Column(TypeName = "date")]
        public DateOnly? EndDate { get; set; }

        public int NumberOfMonths { get; set; }

        public int NumberOfMonthsCreated { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? LastCreatedDate { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal AmountPerMonth { get; set; }

        public bool IsComplete { get; set; }

        [StringLength(50)]
        public string? AccruedType { get; set; }

        [StringLength(2000)]
        public string? Reference { get; set; }

        [NotMapped]
        public List<SelectListItem>? CheckVouchers { get; set; }

        [StringLength(10)]
        public string? CvType { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal CheckAmount { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal AmountPaid { get; set; }

        public bool IsPaid { get; set; }

        [DisplayFormat(DataFormatString = "{0:#,##0.0000;(#,##0.0000)}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal InvoiceAmount { get; set; }

        //Ibs records
        public int? OriginalSupplierId { get; set; }
        public int? OriginalBankId { get; set; }

        public ICollection<CheckVoucherDetail> Details { get; set; }

        [StringLength(100)]
        public string? OldCvNo { get; set; }

        [StringLength(200)]
        public string? SupplierName { get; set; }

        [StringLength(200)]
        public string? BankAccountName { get; set; }

        [StringLength(100)]
        public string? BankAccountNumber { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";


        public bool? IsDocumentedByOtherCompany { get; set; }

        [StringLength(50)]
        public string? DocumentedByCompanyName { get; set; }

        public string? SupportingFileSavedFileName { get; set; }

        public string? SupportingFileSavedUrl { get; set; }

        [Column(TypeName = "date")]
        public DateOnly? DcpDate { get; set; }

        [Column(TypeName = "date")]
        public DateOnly? DcrDate { get; set; }

        public bool IsAdvances { get; set; }

        public bool IsEmployeeAdvance { get; set; }

        [StringLength(200)]
        public string Address { get; set; } = "";

        [StringLength(20)]
        public string Tin { get; set; } = "";

        [StringLength(20)]
        public string VatType { get; set; } = "";

        [StringLength(20)]
        public string TaxType { get; set; } = "";

        [DisplayFormat(DataFormatString = "{0:#,##0.0000;(#,##0.0000)}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal TaxPercent { get; set; }

        public bool IsPayroll { get; set; }

        public DateOnly? LiquidationDate { get; set; }

        public string? ApprovedBy { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? ApprovedDate { get; set; }
    }
}
