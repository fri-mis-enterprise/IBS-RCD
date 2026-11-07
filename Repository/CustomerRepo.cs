using Accounting_System.Data;
using Accounting_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Repository
{
    public class CustomerRepo
    {
        private readonly ApplicationDbContext _dbContext;

        public CustomerRepo(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext
                .Customers
                .ToListAsync(cancellationToken);
        }

        public async Task<Customer> FindCustomerAsync(int? id, CancellationToken cancellationToken = default)
        {
            var customer = await _dbContext
                .Customers
                .FirstOrDefaultAsync(c => c.CustomerId == id, cancellationToken);

            if (customer != null)
            {
                return customer;
            }
            else
            {
                throw new ArgumentException("Invalid id value. The id must be greater than 0.");
            }
        }

        public async Task<bool> IsCustomerExist(string name, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Customers.
                AnyAsync(c => c.CustomerName.ToUpper() == name.ToUpper(), cancellationToken);
        }

        public async Task<bool> IsTinNoExist(string tin, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Customers.
                AnyAsync(c => c.CustomerTin.ToUpper() == tin.ToUpper(), cancellationToken);
        }

        public async Task<int> GetLastNumber(CancellationToken cancellationToken = default)
        {
            var lastNumber = await _dbContext
                .Customers
                .OrderByDescending(s => s.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastNumber != null)
            {
                return lastNumber.Number + 1;
            }
            else
            {
                return 1001;
            }
        }

        public async Task<string> GenerateCodeAsync(string customerType, CancellationToken token)
        {
            var prefix = customerType switch
            {
                "Retail" => "RET",
                "Industrial" => "IND",
                "Reseller" => "RES",
                _ => "GOV"
            };
            var codes = await _dbContext.Customers.Where(c => c.CustomerCode != null && c.CustomerCode.StartsWith(prefix))
                .Select(c => c.CustomerCode!).ToListAsync(token);
            var next = codes.Select(c => int.TryParse(c.AsSpan(3), out var n) ? n : 0).DefaultIfEmpty().Max() + 1;
            if (next > 9999) throw new InvalidOperationException("Customer code range exhausted.");
            return prefix + next.ToString("D4");
        }
    }
}
