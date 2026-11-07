using System.ComponentModel.DataAnnotations;

namespace Accounting_System.Models.MasterFile
{
    public class CustomerBranch
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        [Required, StringLength(50)]
        public string BranchName { get; set; } = string.Empty;
        [Required, StringLength(200)]
        public string BranchAddress { get; set; } = string.Empty;
        [Required, StringLength(50)]
        public string BranchTin { get; set; } = string.Empty;
    }
}
