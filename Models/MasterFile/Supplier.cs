using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Accounting_System.Models.MasterFile
{
    public class Supplier
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierId { get; set; }

        [Display(Name = "Supplier No")]
        public int Number { get; set; }

        [StringLength(100)]
        public string SupplierName { get; set; }

        [StringLength(200)]
        public string SupplierAddress { get; set; }

        [StringLength(20)]
        [Display(Name = "Tin No")]
        [RegularExpression(@"\d{3}-\d{3}-\d{3}-\d{5}", ErrorMessage = "Invalid TIN number format.")]
        public string SupplierTin { get; set; }

        [StringLength(10)]
        public string SupplierTerms { get; set; }

        [StringLength(50)]
        [Display(Name = "VAT Type")]
        public string VatType { get; set; }

        [StringLength(50)]
        [Display(Name = "TAX Type")]
        public string TaxType { get; set; }

        [StringLength(1024)]
        public string? ProofOfRegistrationFilePath { get; set; }

        [Display(Name = "Reason")]
        [StringLength(100)]
        public string? ReasonOfExemption { get; set; }

        [StringLength(20)]
        public string? Validity { get; set; }

        [Display(Name = "Validity Date")]
        [Column(TypeName = "timestamp without time zone")]
        public DateTime? ValidityDate { get; set; }

        [StringLength(2000)]
        public string? ProofOfExemptionFilePath { get; set; }

        [Display(Name = "Created By")]
        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        [Column(TypeName = "timestamp without time zone")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(20)]
        public string Category { get; set; }

        [StringLength(255)]
        public string? TradeName { get; set; }

        [StringLength(20)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Branch { get; set; }

        [StringLength(100)]
        public string? DefaultExpenseNumber { get; set; }

        [NotMapped]
        public List<SelectListItem>? DefaultExpenses { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? WithholdingTaxPercent { get; set; }

        [StringLength(100)]
        public string? WithholdingTaxtitle { get; set; }

        [NotMapped]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? WithholdingTaxTitle { get => WithholdingTaxtitle; set => WithholdingTaxtitle = value; }

        [NotMapped]
        public List<SelectListItem>? WithholdingTaxList { get; set; }

        public int? OriginalSupplierId { get; set; }

        [Required]
        [Display(Name = "Zip Code")]
        [StringLength(10)]
        public string ZipCode { get; set; } = String.Empty;

        [Display(Name = "Supplier Code")]
        [StringLength(7)]
        public string? SupplierCode { get; set; }

        [StringLength(200)]
        public string? ProofOfRegistrationFileName { get; set; }

        [StringLength(200)]
        public string? ProofOfExemptionFileName { get; set; }

        public bool IsActive { get; set; } = true;

        [Display(Name = "Edited By")]
        [StringLength(50)]
        public string? EditedBy { get; set; }

        [Display(Name = "Edited Date")]
        [Column(TypeName = "timestamp without time zone")]
        public DateTime? EditedDate { get; set; }

        [StringLength(50)]
        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        [Display(Name = "Requires Price Adjustment")]
        public bool RequiresPriceAdjustment { get; set; }

        [NotMapped]
        public List<SelectListItem>? PaymentTerms { get; set; }
    }
}
