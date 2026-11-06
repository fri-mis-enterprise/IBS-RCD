# IBS-RCD workbook imports

All 15 `Import` actions use `Repository/WorkbookImport.cs`. `AasImport` and interactive Post/Cancel/Void actions are unchanged. Import orchestration is not registered or implemented in `Program.cs`; the controller entry point activates the coordinator using the existing repository registrations.

## Approved behavior

- Required worksheets are validated before any writes. One serializable transaction covers dependencies, records, details, audits, change logs and financial effects. Failure or cancellation rolls back the complete workbook.
- Existing documents match by original series number; existing master files match by their original IDs. Uploaded values replace mapped fields. Local primary keys and generated local numbers remain stable. Derived settlement fields are reconciled from local transactions.
- Existing changes continue to use the repository comparison/logging methods. New records retain the existing historical audit generation. Master-file updates also log changed mapped fields.
- Mapped document entries are saved as a worksheet batch before status normalization and posting/cancel/void handling. RR `QuantityReceived` retains the value from Excel even when canceled or voided. Those statuses reverse books/inventory and exclude the RR from active PO received totals; they do not erase its document quantity. Both stages share the same transaction.
- Existing books and inventory movements are removed using the saved document references before uploaded values are applied. Posted documents rebuild their effects on every import.
- PostedBy, CanceledBy, VoidedBy, EditedBy and their dates accept independent blank values during parsing. Nonblank dates must still be valid. Automatic posting requires a posting user and date. A valid canceled or voided user/date takes precedence. Cancellation performs void-equivalent reversal, retains canceled status and does not independently set voided status.
- Related documents follow the same workflow as direct imports. Process SI/SV before receipts/memos, PO before RR, RR before CV, and CV before JV. Vouchers post only after their details and payments are saved.
- Imported detail sheets replace omitted details for the included headers. Supplied payment sheets replace payment relationships for the included CV headers; missing optional payment sheets retain existing relationships.

## Accounting boundaries

Existing repository methods retain tax calculations, account selection, posting dates and book entries. PO does not create books. SI includes sales inventory/COGS; RR includes purchase inventory and received quantities. Invoice and voucher identities remain local even when the upload contains source database IDs.

For rebuilding, invoice payments are derived from all active local posted receipts and their offsettings; RR payment totals come from active local CV trade-payment relationships. PO received quantities come from active posted RRs. This repairs missing aggregate effects without subtracting a payment or quantity twice. These calculations treat local source documents as authoritative; they cannot reconstruct transactions absent from the local database.

Inventory balances and affected COGS entries are recalculated from remaining chronological movements. Actual inventory losses subtract stock, gains add stock, and price adjustments retain their value changes. A completed rebuild that would leave negative stock rolls back. Canceling an invoice/RR does not automatically cancel other documents outside the workbook that reference it.

Chart-of-account imports locate columns by header, validate source IDs, and resolve parents by source ID after local accounts exist. An extra status column does not shift identity mapping. Previously misassigned source IDs can be recovered by an unambiguous account-number match while retaining local IDs. Parent changes are compared only after resolution; identical reimports produce no change logs. Invalid or ambiguous identities roll back the workbook. Existing historical logs are not deleted.

Unrelated existing defects in interactive payment helpers and other workbook parsing/calculations remain separate work.

RR and PO parsers locate fields by header as well. They accept Excel date values and date text, skip fully empty rows, and handle blank optional dates without converting empty strings. Missing/invalid required dates report worksheet, row and field. A draft RR's blank received date remains null; active posted RRs still require a received date. Both the older RR column layout and the current 30-column controller export are supported by their named fields.

All 15 IBS-RCD imports now resolve data columns by heading, including voucher details and both payment sheets. `ImportWorksheet.Text`, `Value<T>` and `Date` handle the ordinary mappings; chart-of-account mapping also resolves named headers. Column order and unrelated extra columns do not affect mapping. Header matching ignores case, whitespace and punctuation. Missing required headings and duplicate normalized headings produce explicit errors, with no positional fallback.

Explicit aliases preserve existing export names such as `OriginalSINo`, `OriginalSVNo`, `OriginalCRNo`, `OriginalCMNo`, `OriginalDMNo`, `OriginalCVNo`, `OriginalJVNo`, `CustomerTinNumber`, `CurrentAndPreviousMonth`, `UneranedTitle`, `CVHeaderId` and `JVHeaderId`. The credit-memo export's existing `DebitAmount` label is accepted for `CreditAmount`; the mislabeled PO `OriginalJVNo` in older CV exports is accepted only by the PO parser. The older seven-column CV-detail export omits six optional fields; those retain their existing defaults. Accounting and posting calculations are unchanged by header resolution.

## Verification

Run:

```powershell
dotnet build --no-restore
dotnet run --project Tests/WorkbookImportChecks/WorkbookImportChecks.csproj
```

The checks exercise all 15 imports against an isolated in-memory SQLite database with native test column mappings. Every workbook import reverses column order, changes heading case, adds whitespace to headings and inserts an ignored column. Checks also cover repeat imports, uploaded amount/reference changes, missing books and aggregate effects, payment allocations, inventory, dependency posting, cancellation, voiding and complete rollback. They do not use application connection strings or validate PostgreSQL-specific behavior against a live database.

All nine document parsers require the matching status date when PostedBy, CanceledBy or VoidedBy is supplied, matching RR/PO validation. Missing dates report worksheet, row and field and roll back the workbook. Regression checks also verify canceled/voided SI imports repair previously saved zero quantities and amounts, retain explicit uploaded zeros, omit posting effects, and produce no repeated changes on identical reimports.

Sales Invoice Quantity, UnitPrice and Amount require nonblank cell values. Explicit numeric zero remains valid; null, empty-string and whitespace cells fail with worksheet/row/field context instead of silently overwriting saved values with zero. Discount retains its optional zero default. This parser validation also applies to related SI sheets and AAS imports.

Change logs compare uploaded values against the existing database record before applying the upload, including SI/SV Status. A previous log with the same values does not suppress a current difference. Equal current values produce no log. Payment reconciliation still runs afterward; existing historical logs remain untouched.

When SI/SV worksheets are supplied, payment reconciliation retains their uploaded Status instead of replacing it with a locally calculated payment label. AmountPaid, Balance and IsPaid still reconcile from local receipts. Invoices absent from the workbook retain the existing derived-status behavior. This keeps Excel-versus-database comparisons accurate and makes unchanged receipt workbook reimports produce no new differences.

All 15 import flows are checked by immediately repeating each successful regression import: no new logs and no current-upload changes flag are allowed on an unchanged repeat. Check Voucher amount arrays use the same four-decimal formatting on both comparison sides so equal values such as 0 and 0.0000 do not create logs. The shared current-upload changes flag counts only newly created pending logs, rather than treating historical unresolved logs as new upload differences. Other reviewed import flows required no status-preservation changes.

Expanded field comparison coverage, source-ID handling, optional-field behavior and the current verification commands are documented in docs/ImportDetectionCoverage.md.

Calculated payment fields (AmountPaid, Balance, PaidDate, IsPaid, IsTaxAndVatPaid) are not compared as uploaded document/detail edits. PO received aggregates are likewise excluded. SI/SV/PO/RR no longer parse the recently added aggregate-only DTO fields. Local transaction reconciliation remains authoritative; actual payment-sheet allocation amounts are still parsed and compared. Existing historical logs are retained.

IBS-RCD and AAS ImportAction now handle detected scalar fields, arrays, source references, master records and grouped payment proposals. Calculated fields remain protected. AAS target changes and completion receipts support safe retries when final review-log resolution fails. See docs/ImportActionCoverage.md for approval behavior and isolated verification.

All 15 AasImport upload actions now call WorkbookImport.RunAsync with aas=true. The same parsers, dependency order, comparison snapshots, updates, reversal/posting and reconciliation execute against AAS using its EF model and connection. Identity services stay in the request scope. New proposals commit in AAS with the imported records, then publish by GUID to IBS-RCD for review; pending proposals are republished on the next AAS upload if publication failed. Unlike the former AAS controllers, existing records now follow the regular Import update policy. Status-preservation policies in the shared mappers still apply.

JV and CV exports complete MultipleCheckVoucherPayments dependencies before saving the workbook: both referenced voucher headers and the added vouchers' details are included using source database keys. Existing incomplete workbooks must be re-exported; the importer does not invent a missing invoice or substitute a local AAS key.
