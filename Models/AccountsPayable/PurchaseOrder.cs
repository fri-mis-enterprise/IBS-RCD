using Accounting_System.Models.MasterFile;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accounting_System.Models.AccountsPayable
{
    public class PurchaseOrder : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseOrderId { get; set; }

        [Display(Name = "PO No")]
        [StringLength(13)]
        public string? PurchaseOrderNo { get; set; }

        [Required]
        [Column(TypeName = "date")]
        [Display(Name = "Transaction Date")]
        public DateOnly Date { get; set; }

        [Required]
        [Display(Name = "Supplier Name")]
        public int? SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [NotMapped]
        public List<SelectListItem>? Suppliers { get; set; }

        public int SupplierNo { get; set; }

        [Required]
        [Display(Name = "Product Name")]
        public int? ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [NotMapped]
        public List<SelectListItem>? Products { get; set; }

        [StringLength(15)]
        public string? ProductNo { get; set; }

        [StringLength(10)]
        public string Terms { get; set; }

        [Required]
        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal Quantity { get; set; }

        [Required]
        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal Price { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal Amount { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal? FinalPrice { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "numeric(18,4)")]
        public decimal QuantityReceived { get; set; }

        public bool IsReceived { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime ReceivedDate { get; set; }

        [Required]
        [StringLength(1000)]
        public string Remarks { get; set; }

        public bool IsClosed { get; set; }

        [NotMapped]
        public List<ReceivingReport>? RrList { get; set; }

        //Ibs records
        public int? OriginalSupplierId { get; set; }

        public int? OriginalProductId { get; set; }

        [StringLength(200)]
        public string SupplierName { get; set; } = "";

        [StringLength(200)]
        public string SupplierAddress { get; set; } = "";

        [StringLength(20)]
        public string SupplierTin { get; set; } = "";

        [StringLength(100)]
        public string ProductName { get; set; } = "";

        [StringLength(100)]
        public string? SupplierSalesOrderNo { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public bool IsSubPo { get; set; }

        [StringLength(20)]
        public string? SubPoSeries { get; set; }

        public int? CustomerId { get; set; }

        [StringLength(50)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string OldPoNo { get; set; } = "";


        [Column(TypeName = "date")]
        public DateOnly TriggerDate { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal UnTriggeredQuantity { get; set; }

        public int PickUpPointId { get; set; }

        [StringLength(10)]
        public string VatType { get; set; } = "";

        [StringLength(20)]
        public string TaxType { get; set; } = "";

        [StringLength(4)]
        public string TypeOfPurchase { get; set; } = "";
    }
}
