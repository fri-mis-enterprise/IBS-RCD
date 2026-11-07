using Accounting_System.Data;
using Accounting_System.Models;
using Accounting_System.Models.AccountsPayable;
using Accounting_System.Models.AccountsReceivable;
using Microsoft.EntityFrameworkCore;

namespace Accounting_System.Repository
{
    public static class TransactionDraftFields
    {
        // Only called by draft create/edit actions; posting and ledger routines do not call this helper.
        public static async Task PrepareAsync(ApplicationDbContext db, CancellationToken token)
        {
            var drafts = db.ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified && !e.Entity.IsPosted)
                .Select(e => e.Entity).ToList();
            foreach (var draft in drafts)
            {
                switch (draft)
                {
                    case SalesInvoice invoice:
                        var customer = await db.Customers.FindAsync([invoice.CustomerId], token);
                        if (customer != null)
                        {
                            invoice.CustomerAddress = customer.CustomerAddress;
                            invoice.CustomerTin = customer.CustomerTin;
                            invoice.Terms = customer.CustomerTerms;
                        }
                        break;
                    case ServiceInvoice invoice:
                        var serviceCustomer = await db.Customers.FindAsync([invoice.CustomerId], token);
                        var service = await db.Services.FindAsync([invoice.ServicesId], token);
                        if (serviceCustomer != null)
                        {
                            invoice.CustomerName = serviceCustomer.CustomerName;
                            invoice.CustomerAddress = serviceCustomer.CustomerAddress;
                            invoice.CustomerTin = serviceCustomer.CustomerTin;
                            invoice.CustomerBusinessStyle = serviceCustomer.BusinessStyle;
                            invoice.VatType = serviceCustomer.VatType;
                            invoice.HasEwt = serviceCustomer.WithHoldingTax;
                            invoice.HasWvat = serviceCustomer.WithHoldingVat;
                        }
                        if (service != null)
                        {
                            invoice.ServiceName = service.Name;
                            invoice.ServicePercent = service.Percent;
                        }
                        break;
                    case PurchaseOrder order:
                        var supplier = await db.Suppliers.FindAsync([order.SupplierId], token);
                        var product = await db.Products.FindAsync([order.ProductId], token);
                        if (supplier != null)
                        {
                            order.SupplierName = supplier.SupplierName;
                            order.SupplierAddress = supplier.SupplierAddress;
                            order.SupplierTin = supplier.SupplierTin;
                            order.VatType = supplier.VatType;
                            order.TaxType = supplier.TaxType;
                        }
                        if (product != null) order.ProductName = product.ProductName;
                        if (order.TriggerDate == default) order.TriggerDate = order.Date;
                        break;
                    case CheckVoucherHeader voucher:
                        var payee = voucher.SupplierId == null ? null : await db.Suppliers.FindAsync([voucher.SupplierId], token);
                        var bank = voucher.BankId == null ? null : await db.BankAccounts.FindAsync([voucher.BankId], token);
                        if (payee != null)
                        {
                            voucher.SupplierName = payee.SupplierName;
                            voucher.Address = payee.SupplierAddress;
                            voucher.Tin = payee.SupplierTin;
                            voucher.VatType = payee.VatType;
                            voucher.TaxType = payee.TaxType;
                            voucher.TaxPercent = payee.WithholdingTaxPercent ?? 0;
                        }
                        if (bank != null)
                        {
                            voucher.BankAccountName = bank.AccountName;
                            voucher.BankAccountNumber = bank.AccountNo;
                        }
                        break;
                }
            }
        }
    }
}
