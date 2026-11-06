using System.Globalization;
using Accounting_System.Models;
using Accounting_System.Models.MasterFile;
using Accounting_System.Models.Reports;
using Accounting_System.Utility;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;

namespace Accounting_System.Repository;

public partial class WorkbookImport
{
    private async Task ImportBankAccountAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var sheet = new ImportWorksheet(worksheet);
        var rowCount = worksheet.Dimension.Rows;
        var bankAccountList = await _dbContext
            .BankAccounts
            .ToListAsync(cancellationToken);

        for (int row = 2; row <= rowCount; row++) // Assuming the first row is the header
        {
            if (sheet.IsEmpty(row)) continue;
            var bankAccount = new BankAccount
            {
                Bank = sheet.Text(row, "Bank"),
                AccountName = sheet.Text(row, "AccountName"),
                CreatedBy = sheet.Text(row, "CreatedBy"),
                CreatedDate = sheet.Date(row, "CreatedDate"),
                OriginalBankId = int.TryParse(sheet.Text(row, "OriginalBankId"), out int originalBankId)
                    ? originalBankId
                    : 0,
            };

            var existing = bankAccountList.FirstOrDefault(x => x.OriginalBankId == bankAccount.OriginalBankId);
            if (existing != null)
            {
                await UpdateMasterAsync(existing, bankAccount, "BankAccount", cancellationToken);
                continue;
            }
            bankAccountList.Add(bankAccount);

            await _dbContext.BankAccounts.AddAsync(bankAccount, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }


    private async Task ImportCustomerAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var sheet = new ImportWorksheet(worksheet);
        var rowCount = worksheet.Dimension.Rows;
        var customerList = await _dbContext
            .Customers
            .ToListAsync(cancellationToken);

        for (int row = 2; row <= rowCount; row++)  // Assuming the first row is the header
        {
            if (sheet.IsEmpty(row)) continue;
            var customer = new Customer
            {
                Number = await _customerRepo.GetLastNumber(cancellationToken),
                CustomerName = sheet.Text(row, "CustomerName"),
                CustomerAddress = sheet.Text(row, "CustomerAddress"),
                ZipCode = sheet.Text(row, "ZipCode", "CustomerZipCode"),
                CustomerTin = sheet.Text(row, "CustomerTin", "CustomerTinNumber"),
                BusinessStyle = sheet.Text(row, "BusinessStyle"),
                CustomerTerms = sheet.Text(row, "CustomerTerms", "Terms"),
                CustomerType = sheet.Text(row, "CustomerType"),
                WithHoldingVat = bool.TryParse(sheet.Text(row, "WithHoldingVat"), out bool withHoldingVat) && withHoldingVat,
                WithHoldingTax = bool.TryParse(sheet.Text(row, "WithHoldingTax"), out bool withHoldingTax) && withHoldingTax,
                CreatedBy = sheet.Text(row, "CreatedBy"),
                CreatedDate = sheet.Date(row, "CreatedDate"),
                OriginalCustomerId = int.TryParse(sheet.Text(row, "OriginalCustomerId"), out int customerId) ? customerId : 0,
                OriginalCustomerNumber = sheet.Text(row, "OriginalCustomerNumber"),
            };

            var existing = customerList.FirstOrDefault(x => x.OriginalCustomerId == customer.OriginalCustomerId);
            if (existing != null)
            {
                await UpdateMasterAsync(existing, customer, "Customer", cancellationToken, "Number");
                continue;
            }
            customerList.Add(customer);

            await _dbContext.Customers.AddAsync(customer, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportSupplierAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var sheet = new ImportWorksheet(worksheet);
        var rowCount = worksheet.Dimension.Rows;
        var supplierList = await _dbContext
            .Suppliers
            .ToListAsync(cancellationToken);

        for (int row = 2; row <= rowCount; row++)  // Assuming the first row is the header
        {
            if (sheet.IsEmpty(row)) continue;
            var supplier = new Supplier
            {
                Number = await _supplierRepo.GetLastNumber(cancellationToken),
                SupplierName = sheet.Text(row, "SupplierName", "Name"),
                SupplierAddress = sheet.Text(row, "SupplierAddress", "Address"),
                ZipCode = sheet.Text(row, "ZipCode"),
                SupplierTin = sheet.Text(row, "SupplierTin", "TinNo"),
                SupplierTerms = sheet.Text(row, "SupplierTerms", "Terms"),
                VatType = sheet.Text(row, "VatType"),
                TaxType = sheet.Text(row, "TaxType"),
                ProofOfRegistrationFilePath = sheet.Text(row, "ProofOfRegistrationFilePath"),
                ReasonOfExemption = sheet.Text(row, "ReasonOfExemption"),
                Validity = sheet.Text(row, "Validity"),
                ValidityDate = sheet.Date(row, "ValidityDate"),
                ProofOfExemptionFilePath = sheet.Text(row, "ProofOfExemptionFilePath"),
                CreatedBy = sheet.Text(row, "CreatedBy"),
                CreatedDate = sheet.Date(row, "CreatedDate"),
                Branch = sheet.Text(row, "Branch"),
                Category = sheet.Text(row, "Category"),
                TradeName = sheet.Text(row, "TradeName"),
                DefaultExpenseNumber = sheet.Text(row, "DefaultExpenseNumber"),
                WithholdingTaxPercent = int.TryParse(sheet.Text(row, "WithholdingTaxPercent"), out int withholdingTaxPercent) ? withholdingTaxPercent : 0,
                WithholdingTaxtitle = sheet.Text(row, "WithholdingTaxtitle"),
                OriginalSupplierId = int.TryParse(sheet.Text(row, "OriginalSupplierId"), out int originalSupplierId) ? originalSupplierId : 0,
            };

            var existing = supplierList.FirstOrDefault(x => x.OriginalSupplierId == supplier.OriginalSupplierId);
            if (existing != null)
            {
                await UpdateMasterAsync(existing, supplier, "Supplier", cancellationToken, "Number");
                continue;
            }
            supplierList.Add(supplier);

            await _dbContext.Suppliers.AddAsync(supplier, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportProductAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var sheet = new ImportWorksheet(worksheet);
        var rowCount = worksheet.Dimension.Rows;
        var productList = await _dbContext
            .Products
            .ToListAsync(cancellationToken);

        for (int row = 2; row <= rowCount; row++)  // Assuming the first row is the header
        {
            if (sheet.IsEmpty(row)) continue;
            var product = new Product
            {
                ProductCode = sheet.Text(row, "ProductCode"),
                ProductName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sheet.Text(row, "ProductName").ToLower()),
                ProductUnit = sheet.Text(row, "ProductUnit"),
                CreatedBy = sheet.Text(row, "CreatedBy"),
                CreatedDate = sheet.Date(row, "CreatedDate"),
                OriginalProductId = int.TryParse(sheet.Text(row, "OriginalProductId"), out int originalProductId) ? originalProductId : 0,
            };

            var existing = productList.FirstOrDefault(x => x.OriginalProductId == product.OriginalProductId);
            if (existing != null)
            {
                await UpdateMasterAsync(existing, product, "Product", cancellationToken);
                continue;
            }
            productList.Add(product);

            await _dbContext.Products.AddAsync(product, cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportServiceAsync(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var sheet = new ImportWorksheet(worksheet);
        var rowCount = worksheet.Dimension.Rows;
        var servicesList = await _dbContext
            .Services
            .ToListAsync(cancellationToken);

        for (int row = 2; row <= rowCount; row++)  // Assuming the first row is the header
        {
            if (sheet.IsEmpty(row)) continue;
            var services = new Services
            {
                ServiceNo = await _serviceRepo.GetLastNumber(cancellationToken),
                CurrentAndPreviousTitle = sheet.Text(row, "CurrentAndPreviousTitle"),
                UnearnedTitle = sheet.Text(row, "UnearnedTitle", "UneranedTitle"),
                Name = sheet.Text(row, "Name"),
                Percent = int.TryParse(sheet.Text(row, "Percent"), out int percent) ? percent : 0,
                CreatedBy = sheet.Text(row, "CreatedBy"),
                CreatedDate = sheet.Date(row, "CreatedDate"),
                CurrentAndPreviousNo = sheet.Text(row, "CurrentAndPreviousNo"),
                UnearnedNo = sheet.Text(row, "UnearnedNo"),
                OriginalServiceId = int.TryParse(sheet.Text(row, "OriginalServiceId"), out int originalServiceId) ? originalServiceId : 0,
            };

            var existing = servicesList.FirstOrDefault(x => x.OriginalServiceId == services.OriginalServiceId);
            if (existing != null)
            {
                await UpdateMasterAsync(existing, services, "Service", cancellationToken, "ServiceNo", "CurrentAndPreviousId", "UnearnedId");
                continue;
            }
            servicesList.Add(services);

            await _dbContext.Services.AddAsync(services, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateMasterAsync<TEntity>(TEntity existing, TEntity mapped, string module,
        CancellationToken cancellationToken, params string[] preserve) where TEntity : class
    {
        var entry = _dbContext.Entry(existing);
        var values = _dbContext.Entry(mapped).CurrentValues;
        foreach (var property in entry.Metadata.GetProperties())
        {
            if (property.IsPrimaryKey() || preserve.Contains(property.Name)) continue;
            var original = entry.Property(property.Name).CurrentValue;
            var adjusted = values[property.Name];
            if (Equals(original, adjusted) || (original is null && adjusted is "") || (original is "" && adjusted is null)) continue;
            static string Display(object? value) => value is DateTime date
                ? date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            var before = Display(original);
            var after = Display(adjusted);
            if (_databaseName == "AAS" && existing is ChartOfAccount && property.Name == "ParentAccountId")
            {
                async Task<string> SourceParent(object? value) => value is int id
                    ? (await _dbContext.ChartOfAccounts.Where(x => x.AccountId == id).Select(x => x.OriginalChartOfAccountId).SingleAsync(cancellationToken))?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                    : string.Empty;
                before = await SourceParent(original);
                after = await SourceParent(adjusted);
            }
            _dbContext.ImportExportLogs.Add(new ImportExportLog
            {
                Id = Guid.NewGuid(), TableName = module, Module = module,
                DocumentRecordId = Convert.ToInt32(entry.Property(entry.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue),
                DocumentNo = Convert.ToString(entry.Property(entry.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue) ?? string.Empty,
                ColumnName = property.Name, OriginalValue = before, AdjustedValue = after,
                UploadedBy = _uploadedBy, TimeStamp = DateTime.Now, Action = string.Empty, Executed = false, DatabaseName = _databaseName
            });
        }
        ApplyValues(existing, mapped, preserve);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
