using Accounting_System.Data;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Repository
{
    public class SupplierRepo
    {
        public async Task<string> GenerateCodeAsync(CancellationToken token)
        {
            var codes = await _dbContext.Suppliers.Where(s => s.SupplierCode != null)
                .Select(s => s.SupplierCode!).ToListAsync(token);
            var next = codes.Select(c => c.Length > 1 && int.TryParse(c.AsSpan(1), out var n) ? n : 0)
                .DefaultIfEmpty().Max() + 1;
            if (next > 999999) throw new InvalidOperationException("Supplier code range exhausted.");
            return $"S{next:D6}";
        }
        private readonly ApplicationDbContext _dbContext;

        public SupplierRepo(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> GetLastNumber(CancellationToken cancellationToken = default)
        {
            var lastNumber = await _dbContext
                .Suppliers
                .OrderByDescending(s => s.SupplierId)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastNumber != null)
            {
                return lastNumber.Number + 1;
            }
            else
            {
                return 3001;
            }
        }

        public async Task<bool> IsSupplierNameExist(string supplierName, string category, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Suppliers
                .AnyAsync(s => s.SupplierName.ToUpper() == supplierName.ToUpper() && s.Category == category, cancellationToken);
        }

        public async Task<bool> IsSupplierTinExist(string supplierName, string category, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Suppliers
                .AnyAsync(s => s.SupplierName.ToUpper() == supplierName.ToUpper() && s.Category == category, cancellationToken);
        }
    }
}
