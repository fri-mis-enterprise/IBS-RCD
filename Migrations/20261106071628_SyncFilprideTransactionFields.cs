using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting_System.Migrations
{
    /// <inheritdoc />
    public partial class SyncFilprideTransactionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_amount",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "customer_address",
                table: "service_invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "customer_business_style",
                table: "service_invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_name",
                table: "service_invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "customer_tin",
                table: "service_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_amount_paid",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_balance",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_amount_paid",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_balance",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "debit_amount",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "delivery_receipt_id",
                table: "service_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_ewt",
                table: "service_invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_wvat",
                table: "service_invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "service_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "service_name",
                table: "service_invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "service_percent",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "service_invoices",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "vat_type",
                table: "service_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "credit_amount",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "customer_address",
                table: "sales_invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "customer_order_slip_id",
                table: "sales_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_tin",
                table: "sales_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_amount_paid",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_balance",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_amount_paid",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_balance",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "debit_amount",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "delivery_receipt_id",
                table: "sales_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "sales_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "purchase_order_id",
                table: "sales_invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "receiving_report_id",
                table: "sales_invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "terms",
                table: "sales_invoices",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "sales_invoices",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "authority_to_load_no",
                table: "receiving_reports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_based_on_soa",
                table: "receiving_reports",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "delivery_receipt_id",
                table: "receiving_reports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_cost_updated",
                table: "receiving_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "old_rr_no",
                table: "receiving_reports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "receiving_reports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_dr_no",
                table: "receiving_reports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_percentage",
                table: "receiving_reports",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "receiving_reports",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "withdrawal_certificate",
                table: "receiving_reports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_id",
                table: "purchase_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_sub_po",
                table: "purchase_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "old_po_no",
                table: "purchase_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "pick_up_point_id",
                table: "purchase_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                table: "purchase_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "purchase_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sub_po_series",
                table: "purchase_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_address",
                table: "purchase_orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                table: "purchase_orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_sales_order_no",
                table: "purchase_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_tin",
                table: "purchase_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tax_type",
                table: "purchase_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "trigger_date",
                table: "purchase_orders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "purchase_orders",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type_of_purchase",
                table: "purchase_orders",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "un_triggered_quantity",
                table: "purchase_orders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "vat_type",
                table: "purchase_orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "journal_voucher_headers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_date",
                table: "journal_voucher_headers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "auto_reverse_next_month",
                table: "journal_voucher_headers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "jv_type",
                table: "journal_voucher_headers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "payee",
                table: "journal_voucher_headers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "journal_voucher_headers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "journal_voucher_headers",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sub_account_id",
                table: "journal_voucher_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sub_account_name",
                table: "journal_voucher_details",
                type: "varchar(200)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sub_account_type",
                table: "journal_voucher_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "debit_memos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_date",
                table: "debit_memos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_amount",
                table: "debit_memos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_amount",
                table: "debit_memos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "debit_memos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "debit_memos",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "business_style",
                table: "customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "credit_memos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_date",
                table: "credit_memos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_amount",
                table: "credit_memos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_amount",
                table: "credit_memos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "credit_memos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "credit_memos",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_account_name",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_account_number",
                table: "collection_receipts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "bank_id",
                table: "collection_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "batch_number",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "cleared_date",
                table: "collection_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "cw_vat_period_from",
                table: "collection_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "cw_vat_period_to",
                table: "collection_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cw_vat_reference1",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cw_vat_reference2",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "deposited_date",
                table: "collection_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ewt_period_from",
                table: "collection_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ewt_period_to",
                table: "collection_receipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ewt_reference1",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ewt_reference2",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "f2306file_name",
                table: "collection_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "f2307file_name",
                table: "collection_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "multiple_sv",
                table: "collection_receipts",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "multiple_sv_id",
                table: "collection_receipts",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "collection_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal[]>(
                name: "sv_multiple_amount",
                table: "collection_receipts",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "collection_receipts",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "check_voucher_headers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "check_voucher_headers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_date",
                table: "check_voucher_headers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_account_name",
                table: "check_voucher_headers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_account_number",
                table: "check_voucher_headers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "dcp_date",
                table: "check_voucher_headers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "dcr_date",
                table: "check_voucher_headers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "documented_by_company_name",
                table: "check_voucher_headers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_advances",
                table: "check_voucher_headers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_documented_by_other_company",
                table: "check_voucher_headers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_employee_advance",
                table: "check_voucher_headers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_payroll",
                table: "check_voucher_headers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "liquidation_date",
                table: "check_voucher_headers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_cv_no",
                table: "check_voucher_headers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "check_voucher_headers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                table: "check_voucher_headers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supporting_file_saved_file_name",
                table: "check_voucher_headers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supporting_file_saved_url",
                table: "check_voucher_headers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_percent",
                table: "check_voucher_headers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tax_type",
                table: "check_voucher_headers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tin",
                table: "check_voucher_headers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "check_voucher_headers",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vat_type",
                table: "check_voucher_headers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_display_entry",
                table: "check_voucher_details",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sub_account_id",
                table: "check_voucher_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sub_account_name",
                table: "check_voucher_details",
                type: "varchar(200)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sub_account_type",
                table: "check_voucher_details",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE service_invoices SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE sales_invoices SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE collection_receipts SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE collection_receipts SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE debit_memos SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE debit_memos SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE credit_memos SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE credit_memos SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE purchase_orders SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE purchase_orders SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE receiving_reports SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE receiving_reports SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE check_voucher_headers SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE check_voucher_headers SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE journal_voucher_headers SET type = 'Documented' WHERE type IS NULL OR type = '';");
            migrationBuilder.Sql("UPDATE journal_voucher_headers SET status = CASE WHEN is_canceled THEN 'Canceled' WHEN is_voided THEN 'Voided' WHEN is_posted THEN 'Posted' ELSE 'Pending' END;");
            migrationBuilder.Sql("UPDATE sales_invoices SET payment_status = status;");
            migrationBuilder.Sql("UPDATE service_invoices SET payment_status = status;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_amount",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "customer_address",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "customer_business_style",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "customer_name",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "customer_tin",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "cw_vat_amount_paid",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "cw_vat_balance",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "cwt_amount_paid",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "cwt_balance",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "debit_amount",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "delivery_receipt_id",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "has_ewt",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "has_wvat",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "service_name",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "service_percent",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "type",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "vat_type",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "credit_amount",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "customer_address",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "customer_order_slip_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "customer_tin",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "cw_vat_amount_paid",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "cw_vat_balance",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "cwt_amount_paid",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "cwt_balance",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "debit_amount",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "delivery_receipt_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "purchase_order_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "receiving_report_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "terms",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "type",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "authority_to_load_no",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "cost_based_on_soa",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "delivery_receipt_id",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "is_cost_updated",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "old_rr_no",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "status",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "supplier_dr_no",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "tax_percentage",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "type",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "withdrawal_certificate",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "is_sub_po",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "old_po_no",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "pick_up_point_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "product_name",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "status",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "sub_po_series",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "supplier_address",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "supplier_name",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "supplier_sales_order_no",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "supplier_tin",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "tax_type",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "trigger_date",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "type",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "type_of_purchase",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "un_triggered_quantity",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "vat_type",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "approved_date",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "auto_reverse_next_month",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "jv_type",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "payee",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "status",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "type",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "sub_account_id",
                table: "journal_voucher_details");

            migrationBuilder.DropColumn(
                name: "sub_account_name",
                table: "journal_voucher_details");

            migrationBuilder.DropColumn(
                name: "sub_account_type",
                table: "journal_voucher_details");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "approved_date",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "cw_vat_amount",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "cwt_amount",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "status",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "type",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "approved_date",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "cw_vat_amount",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "cwt_amount",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "status",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "type",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "bank_account_name",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "bank_account_number",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "bank_id",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "batch_number",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "cleared_date",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "cw_vat_period_from",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "cw_vat_period_to",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "cw_vat_reference1",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "cw_vat_reference2",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "deposited_date",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "ewt_period_from",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "ewt_period_to",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "ewt_reference1",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "ewt_reference2",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "f2306file_name",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "f2307file_name",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "multiple_sv",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "multiple_sv_id",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "status",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "sv_multiple_amount",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "type",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "address",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "approved_date",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "bank_account_name",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "bank_account_number",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "dcp_date",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "dcr_date",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "documented_by_company_name",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "is_advances",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "is_documented_by_other_company",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "is_employee_advance",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "is_payroll",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "liquidation_date",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "old_cv_no",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "status",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "supplier_name",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "supporting_file_saved_file_name",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "supporting_file_saved_url",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "tax_percent",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "tax_type",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "tin",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "type",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "vat_type",
                table: "check_voucher_headers");

            migrationBuilder.DropColumn(
                name: "is_display_entry",
                table: "check_voucher_details");

            migrationBuilder.DropColumn(
                name: "sub_account_id",
                table: "check_voucher_details");

            migrationBuilder.DropColumn(
                name: "sub_account_name",
                table: "check_voucher_details");

            migrationBuilder.DropColumn(
                name: "sub_account_type",
                table: "check_voucher_details");

            migrationBuilder.AlterColumn<string>(
                name: "business_style",
                table: "customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
