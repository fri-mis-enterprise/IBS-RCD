# IBS-RCD and AAS import-log actions

`ImportExportLogsController.ImportAction` routes IBS-RCD and AAS records through the shared `IbsImportLogActions` implementation. References resolve in the selected database. The shared RR mapper retains CanceledQuantity when its optional heading is absent and saves it when supplied.

The saved log supplies the target database, table, column, source record ID and proposed value. Table/column query-string values cannot redirect a proposal to another field. Unsupported fields, invalid values, missing references and already reviewed logs do not silently resolve as successful modifications.

## Supported values

- All currently detected document scalar fields, including PostedBy/Date, EditedBy/Date, CanceledBy/Date, VoidedBy/Date, Status and RR CanceledQuantity.
- Detected detail fields, including amount/tax/selection values, supplier IDs, parent header IDs and transaction numbers.
- Bank, customer, supplier, product, service and chart-of-account fields. Local primary keys, generated customer/service numbers and hidden service-account IDs are protected. IBS chart-of-account parent logs already contain resolved local IDs; parent changes validate existence and reject cycles.
- Receipt invoice-number, invoice-ID, amount and date arrays; voucher amount arrays. Entire arrays are parsed, including decimals formatted with thousands separators. Source invoice IDs resolve to local IDs.
- Source document/customer/product/supplier/service references. Their local foreign keys and cached document numbers are updated where applicable. A master record's own original ID remains a source identity field rather than rewriting its primary key.
- Existing invoice-number aliases, receipt amount-correction logs and offsetting fields remain supported.

Calculated AmountPaid, Balance, PaidDate, IsPaid and IsTaxAndVatPaid are not approved as document/detail edits. PO received aggregates, direct lifecycle flags and locally managed fields are likewise excluded. Historical proposals for these fields can be kept; they cannot overwrite locally calculated values through Modify.

## Review behavior

**Modify** applies the logged value and resolves the log with Action=Modify and Executed=true. **Keep** leaves the record unchanged and resolves it with Action=Keep and Executed=false. Invalid changes roll back and remain pending. A resolved proposal cannot be applied twice.

Approved document Status changes synchronize IsPosted, IsCanceled (Canceled or Cancelled), and IsVoided, clearing flags for other statuses. This shared rule covers Sales Invoice and Service Invoice, the only current document models with a Status column. Status matching ignores case and surrounding whitespace. Keep preserves both status and flags. Audit metadata remains separately reviewed. These approvals do not rebuild or reverse journal/inventory books; those remain separate business actions. Existing import mapping policies are unchanged.

Payment-sheet allocation fields are reviewed together so amounts cannot be detached from their report/voucher IDs. New IBS-RCD and AAS payment proposals share one timestamp per group. The confirmation dialog lists the group's fields and values and explicitly offers Modify payment group or Keep payment group. Older payment proposals cannot overwrite newer proposals for the same database/sheet/voucher.

Approved trade-payment groups replace the selected voucher's allocations and reconcile the affected RRs' calculated payment totals/dates. Approved multiple-voucher payment groups update the payment relationships and reconcile affected invoice-voucher payment totals. Additions and removals are supported; incomplete proposals fail without resolving their logs. The value changes, derived totals and resolved logs share one IBS-RCD database transaction.

## Verification

```powershell
dotnet run --project Tests/ImportActionChecks/ImportActionChecks.csproj --configuration Release
```

The checks use separate isolated in-memory SQLite databases for IBS-RCD and AAS. They cover audit fields on all nine document types, Keep/Modify, null dates, status flags, source/local identifiers, detail references, arrays, master fields, payment groups, recalculated totals, invalid proposals and AAS retry receipts. They do not validate PostgreSQL-specific behavior.

AAS chart-of-account parent proposals contain source IDs and resolve to AAS local IDs before cycle validation. IBS-RCD parent proposals already contain local IDs.

AAS records and review logs are in separate databases. Each approved AAS group commits its target changes and completion receipts (the existing ImportExportLogs table, same proposal GUIDs) in one AAS transaction, then resolves the IBS-RCD review logs. If the second commit fails, retry the same action: the receipts finish log resolution without reapplying target changes. A conflicting Keep/Modify retry is rejected. This is recoverable two-step completion, not a distributed atomic transaction.
