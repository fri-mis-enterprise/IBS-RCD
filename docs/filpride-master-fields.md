# Filpride master fields in IBS-RCD

Source: the adjacent IBSWeb repository's Filpride master models, repositories, controllers, and views.

| Master | Added behavior |
| --- | --- |
| Customers | Customer code, separate VAT type, active status, edit audit fields, cluster/station, credit limits, branch records and management, retention, multiple-terms flag, BIR upload/download, price-adjustment flag, commissionee/rate, configurable withholding rates |
| Products | Active status and edit audit fields |
| Services | Reviewed; the source has no additional persisted fields. Existing RCD service numbering is retained. Hidden accounts are excluded from account selection. |
| Suppliers | Supplier code, active status, edit audit fields, employee number, document filenames, fractional withholding percentage, price-adjustment flag, Commissionee/Hauler/Employee categories |
| Bank accounts | Branch, account number, active status; account numbers appear in payment selection and exports |
| Chart of accounts | Hidden status and financial statement classification; children inherit classification from their parent |

The existing invoice, memo, printing, posting, payment selection, and workbook paths use the applicable new fields. Withholding rates are captured on invoices; legacy invoice rates remain 1% and 5%. Older workbooks preserve added fields they do not contain. Customer commissionee references in imports resolve using the supplier's original source ID. Workbook imports do not transfer customer document binaries.

## Database deployment

The migrations are prepared, not applied:

1. `SyncFilprideMasterFields`
2. `SnapshotMasterWithholdingRates`

Apply with `dotnet ef database update --context ApplicationDbContext` in the intended deployment environment. The master migration retains existing VAT treatment, enables existing records, and generates customer/supplier codes. Existing customer types that held VAT classifications become `Industrial`; review their commercial categories after deployment. Existing bank branch/account values start empty and can be completed in Edit.

The application also maps these entity types through `AasDbContext`. Its external database must have matching schema before using the affected AAS workflows; the ApplicationDbContext migrations do not update the AAS connection automatically.

BIR documents are stored under `App_Data/CustomerDocuments`, outside the public web root. Include that directory in deployment storage and backups.

## Source workflows without an RCD counterpart

Filpride's customer order slips, delivery receipts, supplier appointment/price-adjustment processes, and GL period/subaccount balance modules are absent from IBS-RCD. Their master settings are persisted, but these missing transaction modules were not transplanted. Credit-limit enforcement, commission calculation, retention/multiple-term processing, and price-adjustment execution therefore do not acquire Filpride's absent downstream workflows. Customer branches have master management and a lookup endpoint; existing RCD invoices do not acquire Filpride delivery-receipt branch selection.

## Verification

Run:

```text
dotnet build Accounting_System.sln
dotnet run --project Tests/MasterFileChecks/MasterFileChecks.csproj
dotnet run --project Tests/ImportActionChecks/ImportActionChecks.csproj
dotnet run --project Tests/ImportDetectionChecks/ImportDetectionChecks.csproj
```

The executable checks use isolated SQLite databases. They do not validate production PostgreSQL migration execution or browser interactions.
