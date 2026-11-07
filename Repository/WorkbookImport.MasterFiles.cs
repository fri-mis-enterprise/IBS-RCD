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

            if (sheet.HasColumn("Branch")) bankAccount.Branch = sheet.Text(row, "Branch");
            if (sheet.HasColumn("AccountNo")) bankAccount.AccountNo = sheet.Text(row, "AccountNo");
            if (sheet.HasColumn("IsActive")) bankAccount.IsActive = sheet.Value<bool>(row, "IsActive");
            var existing = bankAccountList.FirstOrDefault(x => x.OriginalBankId == bankAccount.OriginalBankId);
            if (existing != null)
            {
                if (!sheet.HasColumn("Branch")) bankAccount.Branch = existing.Branch;
                if (!sheet.HasColumn("AccountNo")) bankAccount.AccountNo = existing.AccountNo;
                if (!sheet.HasColumn("IsActive")) bankAccount.IsActive = existing.IsActive;
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

        var sourceIds = new HashSet<int>();
        for (var sourceRow = 2; sourceRow <= rowCount; sourceRow++)
        {
            if (sheet.IsEmpty(sourceRow)) continue;
            var sourceId = sheet.RequiredValue<int>(sourceRow, "OriginalCustomerId");
            if (sourceId <= 0 || !sourceIds.Add(sourceId))
                throw new InvalidOperationException($"Customer row {sourceRow}: original customer IDs must be positive and unique.");
            var matches = customerList.Where(c => c.OriginalCustomerId == sourceId).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException($"Multiple customers use original customer ID {sourceId}. Resolve the source mapping before importing.");
            var sourceName = sheet.Text(sourceRow, "CustomerName");
            if (sheet.HasColumn("Type") && !string.IsNullOrWhiteSpace(sheet.Text(sourceRow, "Type"))
                && !string.Equals(sheet.Text(sourceRow, "Type"), "Documented", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Customer row {sourceRow}: IBS-RCD accepts documented customers only.");
            if (matches.Count == 1 && !string.Equals(matches[0].CustomerName.Trim(), sourceName.Trim(), StringComparison.OrdinalIgnoreCase)
                && customerList.Any(c => c.CustomerId != matches[0].CustomerId && string.Equals(c.CustomerName.Trim(), sourceName.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Customer row {sourceRow}: source ID {sourceId} belongs to a different local customer. The workbook's customer IDs do not match the existing source mapping. No customers were imported.");
        }

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

            if (sheet.HasColumn("CustomerCode")) customer.CustomerCode = sheet.Text(row, "CustomerCode");
            if (sheet.HasColumn("VatType")) customer.VatType = sheet.Text(row, "VatType");
            if (sheet.HasColumn("IsActive")) customer.IsActive = sheet.Value<bool>(row, "IsActive");
            if (sheet.HasColumn("EditedBy")) customer.EditedBy = sheet.Text(row, "EditedBy");
            if (sheet.HasColumn("EditedDate")) customer.EditedDate = string.IsNullOrWhiteSpace(sheet.Text(row, "EditedDate")) ? null : sheet.Date(row, "EditedDate");
            if (sheet.HasColumn("ClusterCode")) customer.ClusterCode = string.IsNullOrWhiteSpace(sheet.Text(row, "ClusterCode")) ? null : Enum.Parse<ClusterArea>(sheet.Text(row, "ClusterCode"), true);
            if (sheet.HasColumn("StationCode")) customer.StationCode = sheet.Text(row, "StationCode");
            if (sheet.HasColumn("CreditLimit")) customer.CreditLimit = sheet.Value<decimal>(row, "CreditLimit");
            if (sheet.HasColumn("CreditLimitAsOfToday")) customer.CreditLimitAsOfToday = sheet.Value<decimal>(row, "CreditLimitAsOfToday");
            if (sheet.HasColumn("HasBranch")) customer.HasBranch = sheet.Value<bool>(row, "HasBranch");
            if (sheet.HasColumn("RetentionRate")) customer.RetentionRate = sheet.Value<decimal?>(row, "RetentionRate");
            if (sheet.HasColumn("HasMultipleTerms")) customer.HasMultipleTerms = sheet.Value<bool>(row, "HasMultipleTerms");
            if (sheet.HasColumn("RequiresPriceAdjustment")) customer.RequiresPriceAdjustment = sheet.Value<bool>(row, "RequiresPriceAdjustment");
            if (sheet.HasColumn("CommissionRate")) customer.CommissionRate = sheet.Value<decimal>(row, "CommissionRate");
            if (sheet.HasColumn("CwtPercent")) customer.CwtPercent = sheet.Value<decimal>(row, "CwtPercent");
            if (sheet.HasColumn("CwVatPercent")) customer.CwVatPercent = sheet.Value<decimal>(row, "CwVatPercent");
            if (!sheet.HasColumn("VatType") && customer.CustomerType is "Vatable" or "Zero Rated" or "Zero-Rated" or "Exempt") customer.VatType = customer.CustomerType;
            var existing = customerList.FirstOrDefault(x => x.OriginalCustomerId == customer.OriginalCustomerId);
            if (customer.CustomerType is "Vatable" or "Zero Rated" or "Zero-Rated" or "Exempt")
            {
                customer.VatType = customer.CustomerType;
                customer.CustomerType = existing?.CustomerType is "Retail" or "Industrial" or "Government" or "Reseller"
                    ? existing.CustomerType : "Industrial";
            }
            if (sheet.HasColumn("CommissioneeId") && sheet.Value<int?>(row, "CommissioneeId") is int sourceCommissioneeId)
                customer.CommissioneeId = await _dbContext.Suppliers.Where(s => s.OriginalSupplierId == sourceCommissioneeId)
                    .Select(s => (int?)s.SupplierId).SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Customer row {row}: import commissionee supplier {sourceCommissioneeId} first.");
            else
                customer.CommissioneeId = existing?.CommissioneeId;
            // A workbook transfers document metadata, not the locally stored document binary.
            customer.BirDocumentFilePath = existing?.BirDocumentFilePath;
            customer.BirDocumentFileName = existing?.BirDocumentFileName;
            if (existing != null)
            {
                if (!sheet.HasColumn("CustomerCode")) customer.CustomerCode = existing.CustomerCode;
                if (!sheet.HasColumn("VatType") && sheet.Text(row, "CustomerType") is not ("Vatable" or "Zero Rated" or "Zero-Rated" or "Exempt")) customer.VatType = existing.VatType;
                if (!sheet.HasColumn("IsActive")) customer.IsActive = existing.IsActive;
                if (!sheet.HasColumn("EditedBy")) customer.EditedBy = existing.EditedBy;
                if (!sheet.HasColumn("EditedDate")) customer.EditedDate = existing.EditedDate;
                if (!sheet.HasColumn("ClusterCode")) customer.ClusterCode = existing.ClusterCode;
                if (!sheet.HasColumn("StationCode")) customer.StationCode = existing.StationCode;
                if (!sheet.HasColumn("CreditLimit")) customer.CreditLimit = existing.CreditLimit;
                if (!sheet.HasColumn("CreditLimitAsOfToday")) customer.CreditLimitAsOfToday = existing.CreditLimitAsOfToday;
                if (!sheet.HasColumn("HasBranch")) customer.HasBranch = existing.HasBranch;
                if (!sheet.HasColumn("RetentionRate")) customer.RetentionRate = existing.RetentionRate;
                if (!sheet.HasColumn("HasMultipleTerms")) customer.HasMultipleTerms = existing.HasMultipleTerms;
                if (!sheet.HasColumn("RequiresPriceAdjustment")) customer.RequiresPriceAdjustment = existing.RequiresPriceAdjustment;
                if (!sheet.HasColumn("CommissionRate")) customer.CommissionRate = existing.CommissionRate;
                if (!sheet.HasColumn("CwtPercent")) customer.CwtPercent = existing.CwtPercent;
                if (!sheet.HasColumn("CwVatPercent")) customer.CwVatPercent = existing.CwVatPercent;
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
                WithholdingTaxPercent = sheet.Value<decimal?>(row, "WithholdingTaxPercent"),
                WithholdingTaxtitle = sheet.Text(row, "WithholdingTaxtitle"),
                OriginalSupplierId = int.TryParse(sheet.Text(row, "OriginalSupplierId"), out int originalSupplierId) ? originalSupplierId : 0,
            };

            if (sheet.HasColumn("SupplierCode")) supplier.SupplierCode = sheet.Text(row, "SupplierCode");
            if (sheet.HasColumn("IsActive")) supplier.IsActive = sheet.Value<bool>(row, "IsActive");
            if (sheet.HasColumn("EditedBy")) supplier.EditedBy = sheet.Text(row, "EditedBy");
            if (sheet.HasColumn("EditedDate")) supplier.EditedDate = sheet.Date(row, "EditedDate");
            if (sheet.HasColumn("EmployeeNumber")) supplier.EmployeeNumber = sheet.Text(row, "EmployeeNumber");
            if (sheet.HasColumn("RequiresPriceAdjustment")) supplier.RequiresPriceAdjustment = sheet.Value<bool>(row, "RequiresPriceAdjustment");
            if (sheet.HasColumn("ProofOfRegistrationFileName")) supplier.ProofOfRegistrationFileName = sheet.Text(row, "ProofOfRegistrationFileName");
            if (sheet.HasColumn("ProofOfExemptionFileName")) supplier.ProofOfExemptionFileName = sheet.Text(row, "ProofOfExemptionFileName");
            var existing = supplierList.FirstOrDefault(x => x.OriginalSupplierId == supplier.OriginalSupplierId);
            if (existing != null)
            {
                if (!sheet.HasColumn("SupplierCode")) supplier.SupplierCode = existing.SupplierCode;
                if (!sheet.HasColumn("IsActive")) supplier.IsActive = existing.IsActive;
                if (!sheet.HasColumn("EditedBy")) supplier.EditedBy = existing.EditedBy;
                if (!sheet.HasColumn("EditedDate")) supplier.EditedDate = existing.EditedDate;
                if (!sheet.HasColumn("EmployeeNumber")) supplier.EmployeeNumber = existing.EmployeeNumber;
                if (!sheet.HasColumn("RequiresPriceAdjustment")) supplier.RequiresPriceAdjustment = existing.RequiresPriceAdjustment;
                if (!sheet.HasColumn("ProofOfRegistrationFileName")) supplier.ProofOfRegistrationFileName = existing.ProofOfRegistrationFileName;
                if (!sheet.HasColumn("ProofOfExemptionFileName")) supplier.ProofOfExemptionFileName = existing.ProofOfExemptionFileName;
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

            if (sheet.HasColumn("IsActive")) product.IsActive = sheet.Value<bool>(row, "IsActive");
            if (sheet.HasColumn("EditedBy")) product.EditedBy = sheet.Text(row, "EditedBy");
            if (sheet.HasColumn("EditedDate")) product.EditedDate = sheet.Date(row, "EditedDate");
            var existing = productList.FirstOrDefault(x => x.OriginalProductId == product.OriginalProductId);
            if (existing != null)
            {
                if (!sheet.HasColumn("IsActive")) product.IsActive = existing.IsActive;
                if (!sheet.HasColumn("EditedBy")) product.EditedBy = existing.EditedBy;
                if (!sheet.HasColumn("EditedDate")) product.EditedDate = existing.EditedDate;
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
