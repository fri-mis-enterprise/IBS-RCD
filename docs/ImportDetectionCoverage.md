# Workbook comparison coverage

Comparisons use the existing database values before reversal, mapping, or reconciliation changes them. A new record has no previous values to compare and retains the existing audit workflow. Related worksheets use the same comparisons as standalone imports.

| Worksheet | Comparison coverage added or corrected |
| --- | --- |
| SalesInvoice | EditedBy/Date, CanceledBy/Date, VoidedBy/Date; calculated payment fields excluded |
| ServiceInvoice | EditedBy/Date, CanceledBy/Date, VoidedBy/Date; calculated payment fields excluded |
| PurchaseOrder | EditedBy/Date, CanceledBy/Date, VoidedBy/Date; calculated received totals/dates excluded |
| ReceivingReport | EditedBy/Date, CanceledBy/Date, VoidedBy/Date; CanceledQuantity when supplied; SupplierInvoiceDate now compares Excel against the database; calculated payment fields excluded |
| CollectionReceipt | EditedBy/Date, CanceledBy/Date, VoidedBy/Date; all invoice/allocation/date arrays compare their contents, with local invoice IDs projected to source IDs |
| CreditMemo / DebitMemo | EditedBy/Date, CanceledBy/Date, VoidedBy/Date |
| CheckVoucherHeader | EditedBy/Date, CanceledBy/Date, VoidedBy/Date; Reference compared using the source voucher number |
| JournalVoucherHeader | EditedBy/Date, CanceledBy/Date, VoidedBy/Date |
| CheckVoucherDetails | SupplierId and TransactionNo; entered amount/tax/selection comparisons run only for optional headings actually present. AmountPaid is excluded. Older seven-column sheets retain omitted values |
| JournalVoucherDetails | Source JournalVoucherHeaderId and TransactionNo |
| CheckVoucherTradePayments | DocumentId, DocumentType, CheckVoucherId, AmountPaid; added/removed relationships are compared before replacement |
| MultipleCheckVoucherPayments | Payment/invoice source IDs and decimal AmountPaid; added/removed relationships are compared before replacement |
| BankAccount / Customers / Supplier / Product / Services / ChartOfAccount | The IBS-RCD mapped-field comparison already covers supplied master values. AAS imports now also log existing-record differences rather than silently skipping comparison. AAS master fields resolve by heading |

Existing repository Detect methods cover their previously compared fields as well, except the calculated fields excluded below. Shared repository comparisons apply to IBS-RCD and AAS; payment comparison explicitly selects the target database. Locally generated customer/service numbers and service account IDs absent from Excel are preserved.

Payment-sheet row `Id` values are local generated identities: the models do not retain a separate original payment-row ID. Payments are compared by source voucher/report relationships instead. BankAccount `Branch`/`AccountNo` and SalesInvoice `OriginalReceivingReportId`/`OriginalPOId` are legacy export headings without corresponding stored fields in this application's models; they cannot be compared against a database value. Extra headings from other application versions without a stored counterpart are likewise not invented as database fields.

Calculated payment fields (AmountPaid, Balance, PaidDate, IsPaid and IsTaxAndVatPaid) are excluded from document/detail Detect methods. PO QuantityReceived, IsReceived and ReceivedDate are also excluded because uploads reconcile them from RRs. The recently added optional parser/DTO fields for these aggregates have been removed from SI/SV/PO/RR. Actual document amounts, RR QuantityReceived/ReceivedDate, and payment-sheet allocation AmountPaid values remain compared because they are transaction inputs. PO ReceivedDate and RR PaidDate are now rebuilt from source transaction dates rather than the time of reupload.

Timestamp comparisons and exports use 24-hour time and seven fractional digits. Import-action date parsing accepts both the new format and the previous six-digit format. Blank optional dates match null database dates. Existing files exported in 12-hour time without AM/PM have already lost that information; comparison does not guess the missing half-day.

Detection preserves the current Sales Invoice mapping/status policy. IBS-RCD and AAS approval support for detected columns is described in [ImportActionCoverage.md](ImportActionCoverage.md). Posting/cancel/void remain separate business actions.

## Verification

```powershell
dotnet build --no-restore --configuration Release
dotnet run --project Tests/ImportDetectionChecks/ImportDetectionChecks.csproj --configuration Release
```

The isolated SQLite checks exercise 272 header/detail fields for equality and change, payment fields, source-ID projection, missing optional fields, pre-reversal aggregates, timestamps, receipt arrays, all six IBS master imports, and all six AAS master controller actions with reordered headings. No application connection strings are used.

The older WorkbookImportChecks suite assumes reuploads immediately apply changed Sales Invoice lifecycle metadata. The current status-preserving mapper no longer satisfies that assumption; that suite is not reported as passing by these detection checks.

Reupload checks also verify stale calculated payment/received totals never generate differences while actual allocation changes remain detected.

AAS master imports retain existing values until review. An unchanged reupload does not add another pending proposal for the same database, table, record, column, original value and proposed value, or raise a new-changes notification for that duplicate. A different proposed value remains reviewable. Existing historical logs are retained. Source IDs identify records; different product codes/names under the same OriginalProductId remain real differences requiring review.

Journal Voucher's Check Voucher reference is optional: blank/zero OriginalCVId maps to null CVId. A supplied nonzero source ID must resolve to a Check Voucher. The shared mapper applies this rule to IBS-RCD and AAS. The regression checks exercise upload and reupload through all nine AAS document controllers, standalone JV header/detail linking, and valid/missing CV references.

All nine AAS document upload actions compare pending AAS log counts before and after the upload for the import-changes notification. Historical pending Product/master logs alone do not trigger a new-changes notification after a JV or other document upload. The review list still displays unresolved historical proposals.
