# IBSWeb transaction port: implementation status

The source is the sibling IBSWeb solution. IBS-RCD's existing posting, void,
cancellation, books and general ledger implementations are retained. The work
below is a partial port; it is not full workflow parity with IBSWeb.

## Implemented

- Added the missing scalar transaction fields for the AR/AP modules, including
  invoice/master snapshots, receipt withholding references, voucher documentation,
  approval metadata and detail subaccount metadata.
- Draft create/edit captures customer, supplier, service, product and bank snapshot
  fields without creating accounting entries. Posted snapshots are not refreshed.
- Optional new header columns are exported/imported. Old workbooks retain existing
  supplemental values when those columns are absent. Undocumented input is rejected.
  Source manager-check header aliases are accepted.
- Debit/credit memo and receiving report Create/Edit use the IBSWeb form structure,
  adapted to existing RCD model and endpoint names. Memo source toggling disables
  inactive required controls.
- Purchase order additional inputs, receipt withholding inputs, and voucher
  documentation inputs are saved by existing RCD draft workflows. Voucher company
  toggles and supporting-document persistence/download are connected.
- Receipt deposit, redeposit, return and clearing metadata is available on posted
  receipt screens. These actions do not create financial entries; RCD's existing
  posting action continues to own accounting entries.
- Invoice and purchase order print headers use stored master snapshots, with a
  fallback for historical blank snapshots.

## Remaining source dependencies and workflows

- IBSWeb Sales Invoice generation depends on Customer Order Slip and Delivery
  Receipt modules absent from RCD. Service transaction-fee logic also uses DRs.
- Source purchase-order pickup points and integrated price-trigger workflows need
  their source dependencies.
- Multiple service collections and their allocation-detail workflow are not yet
  ported. Added service-array columns alone do not implement that workflow.
- Source approval roles/actions are not yet ported; stored approval columns alone
  do not implement approvals.
- Voucher employee/supplier advances, commissions and hauler-payment workflows
  require additional source modules. Detail subaccount inputs and detail workbook
  mapping are not yet connected; only their schema fields are present.
- Specialized journal workflows (accrual, amortization, liquidation and reclass)
  include source automatic posting/reversals. They require a separate adaptation
  that preserves RCD posting. The new journal metadata does not activate them.
- Existing Sales/Service Invoice, voucher and journal forms have not all been
  replaced with complete IBSWeb form/JavaScript implementations.

## Database

Follow-up migration `20261106074926_RemoveDocumentClassificationType` removes
the Documented/Undocumented `type` column from customers and all nine transaction
tables. The models and exports no longer contain that classification. Legacy
workbooks may omit Type or use Documented; Undocumented input is still rejected.
VAT, customer, account, purchase and voucher classifications remain intact.
This follow-up migration is generated, not applied.

Migration `20261106071628_SyncFilprideTransactionFields` is generated but not applied.
Its Up operation adds transaction fields, allows the already-optional customer
business style, and backfills new document/status metadata. It does not modify
existing book/ledger tables, financial balances, or lifecycle flags. Apply through
the normal database deployment process after reviewing the migration.

Raw IDs for source-only integrations are stored as supplemental metadata; they
are not connected RCD relationships. External supporting-file URLs do not transfer
the underlying file into RCD.

## Validation

- `node Tests/TransactionPortChecks/posting-check.cjs`: hashes 51 protected RCD
  methods against the captured pre-port baseline.
- `dotnet run --project Tests/TransactionPortChecks`: SQLite tests for draft
  snapshots, posted isolation, no book writes, optional workbook preservation,
  supplemental Excel round trips, source arrays and voucher documentation.
- The same project with `-- render <output-directory>` renders transaction forms
  using the compiled Razor views. `forms.cjs <output-directory>` checks memo and
  voucher toggles with jsdom/jQuery (JSDOM_PATH and JQUERY_PATH select dependencies).
- Existing ImportDetectionChecks, MasterFileChecks and ImportActionChecks passed.
  The action-test posted payer is explicitly restored after the generic status
  round-trip now that vouchers also expose a Status property.
- No live browser is available in this session; rendered HTML/DOM checks are not
  a visual browser review or a production database verification.
