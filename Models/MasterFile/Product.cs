using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Accounting_System.Models.MasterFile
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [StringLength(10)]
        public string? ProductCode { get; set; }
        [StringLength(50)]
        public string ProductName { get; set; }
        [StringLength(2)]
        public string ProductUnit { get; set; }

        [Display(Name = "Created By")]
        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        [Column(TypeName = "timestamp without time zone")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int OriginalProductId { get; set; }

        public bool IsActive { get; set; } = true;

        [Display(Name = "Edited By")]
        [Column(TypeName = "varchar(50)")]
        public string? EditedBy { get; set; }

        [Display(Name = "Edited Date")]
        [Column(TypeName = "timestamp without time zone")]
        public DateTime? EditedDate { get; set; }
    }
}
